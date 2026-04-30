"""CCCD/CMND OCR extraction service."""

from __future__ import annotations

import re
import threading

import structlog

from ocr.models.response import CCCDExtraction, IdCardSuggestedValues
from ocr.services.name_normalizer import normalize_vietnamese_name, normalize_address, parse_vietnamese_date

logger = structlog.get_logger(__name__)

# Vietnamese CCCD patterns
_RE_ID_NUMBER = re.compile(r"\b(\d{9}|\d{12})\b")
_RE_NAME = re.compile(r"(?:Họ\s+và\s+tên|Ho\s+va\s+ten)[:\s]+([A-ZÀÁÂÃÈÉÊÌÍÒÓÔÕÙÚÝĂĐƠƯẠẶẬẦẤẮẢÃÊỆÊỂỆIẾIỊOỌỘỐỒỔƠỢỦỤỨỪ\s]+)", re.IGNORECASE)
_RE_DOB = re.compile(r"(?:Ngày\s+sinh|Ngay\s+sinh)[:\s]+(\d{2}[/-]\d{2}[/-]\d{4})", re.IGNORECASE)
_RE_GENDER = re.compile(r"(?:Giới\s+tính|Gioi\s+tinh)[:\s]+(Nam|Nữ|Nu)", re.IGNORECASE)
_RE_NATIONALITY = re.compile(r"(?:Quốc\s+tịch|Quoc\s+tich)[:\s]+(\S+(?:\s+\S+)?)", re.IGNORECASE)
_RE_ORIGIN = re.compile(r"(?:Quê\s+quán|Que\s+quan)[:\s]+(.+?)(?=Nơi|Có|$)", re.IGNORECASE | re.DOTALL)
_RE_RESIDENCE = re.compile(r"(?:Nơi\s+thường\s+trú|Noi\s+thuong\s+tru)[:\s]+(.+?)(?=Có|Ngày|$)", re.IGNORECASE | re.DOTALL)
_RE_EXPIRY = re.compile(r"(?:Có\s+giá\s+trị\s+đến|Co\s+gia\s+tri\s+den)[:\s]+(\d{2}[/-]\d{2}[/-]\d{4})", re.IGNORECASE)


class IdCardOcrService:
    _instance: IdCardOcrService | None = None
    _lock = threading.Lock()

    def __init__(self) -> None:
        from paddleocr import PaddleOCR
        self._ocr = PaddleOCR(use_angle_cls=True, lang="vi", show_log=False)
        logger.info("id_card_ocr_initialized")

    @classmethod
    def get_instance(cls) -> IdCardOcrService:
        if cls._instance is None:
            with cls._lock:
                if cls._instance is None:
                    cls._instance = cls()
        return cls._instance

    def _run_ocr(self, image_array) -> str:
        result = self._ocr.ocr(image_array, cls=True)
        lines: list[str] = []
        if result and result[0]:
            for line in result[0]:
                if line and len(line) >= 2:
                    text, _ = line[1]
                    lines.append(text)
        return "\n".join(lines)

    def _extract_field(self, pattern: re.Pattern, text: str, group: int = 1) -> str:
        m = pattern.search(text)
        if m:
            return m.group(group).strip()
        return ""

    def _confidence_from_fields(self, fields: dict[str, str]) -> float:
        """Compute confidence as ratio of non-empty critical fields."""
        critical = ["id_number", "full_name", "date_of_birth", "expiry_date"]
        filled = sum(1 for f in critical if fields.get(f))
        return round(filled / len(critical), 2)

    async def extract(self, front_image, back_image) -> CCCDExtraction:
        import asyncio
        from functools import partial

        loop = asyncio.get_event_loop()
        front_text = await loop.run_in_executor(None, partial(self._run_ocr, front_image))
        back_text = await loop.run_in_executor(None, partial(self._run_ocr, back_image))
        full_text = front_text + "\n" + back_text

        log = logger.bind(raw_length=len(full_text))

        id_number = self._extract_field(_RE_ID_NUMBER, full_text)
        full_name = self._extract_field(_RE_NAME, full_text)
        dob_raw = self._extract_field(_RE_DOB, full_text)
        gender = self._extract_field(_RE_GENDER, full_text)
        nationality = self._extract_field(_RE_NATIONALITY, full_text) or "Việt Nam"
        origin = self._extract_field(_RE_ORIGIN, full_text)
        residence = self._extract_field(_RE_RESIDENCE, full_text)
        expiry_raw = self._extract_field(_RE_EXPIRY, full_text)

        dob = parse_vietnamese_date(dob_raw) or ""
        expiry = parse_vietnamese_date(expiry_raw) or ""
        first_name, normalized_name = normalize_vietnamese_name(full_name)

        fields = {
            "id_number": id_number,
            "full_name": full_name,
            "date_of_birth": dob,
            "expiry_date": expiry,
        }
        confidence = self._confidence_from_fields(fields)

        log.info("id_card_extracted", id_number_found=bool(id_number), confidence=confidence)

        return CCCDExtraction(
            id_number=id_number,
            full_name=full_name,
            date_of_birth=dob,
            gender=gender,
            nationality=nationality,
            place_of_origin=origin.strip(),
            place_of_residence=residence.strip(),
            expiry_date=expiry,
            confidence=confidence,
            raw_text=full_text,
            suggested_form_values=IdCardSuggestedValues(
                first_name=first_name,
                last_name=normalized_name,
                date_of_birth=dob,
                address=normalize_address(residence),
            ) if normalized_name else None,
        )
