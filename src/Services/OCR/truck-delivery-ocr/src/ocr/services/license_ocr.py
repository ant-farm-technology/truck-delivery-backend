"""Giấy phép lái xe (GPLX) OCR extraction service."""

from __future__ import annotations

import re
import threading

import structlog

from ocr.models.response import LicenseExtraction, LicenseSuggestedValues
from ocr.services.name_normalizer import normalize_vietnamese_name, normalize_address, parse_vietnamese_date

logger = structlog.get_logger(__name__)

_RE_LICENSE_NUM = re.compile(r"\b(\d{12})\b")
_RE_NAME = re.compile(r"(?:Họ\s+và\s+tên|Ho\s+va\s+ten)[:\s]+([A-ZÀÁÂÃÈÉÊÌÍÒÓÔÕÙÚÝĂĐƠƯẠẶẬẦẤẮẢÃÊỆỂẾIỊOỌỘỐỒỔƠỢỦỤỨỪ\s]+)", re.IGNORECASE)
_RE_DOB = re.compile(r"(?:Ngày\s+sinh|Ngay\s+sinh)[:\s]+(\d{2}[/-]\d{2}[/-]\d{4})", re.IGNORECASE)
_RE_ADDRESS = re.compile(r"(?:Nơi\s+cư\s+trú|Noi\s+cu\s+tru|Địa\s+chỉ|Dia\s+chi)[:\s]+(.+?)(?=Hạng|Ha ng|Ngày|$)", re.IGNORECASE | re.DOTALL)
_RE_GRADE = re.compile(r"(?:Hạng|Hang)[:\s]+(B1|B2|C|D|E|FC|FD|A1|A2|A3|A4)", re.IGNORECASE)
_RE_ISSUE = re.compile(r"(?:Ngày\s+cấp|Ngay\s+cap)[:\s]+(\d{2}[/-]\d{2}[/-]\d{4})", re.IGNORECASE)
_RE_EXPIRY = re.compile(r"(?:Ngày\s+hết\s+hạn|Ngay\s+het\s+han|Có\s+giá\s+trị\s+đến)[:\s]+(\d{2}[/-]\d{2}[/-]\d{4})", re.IGNORECASE)
_RE_AUTHORITY = re.compile(r"(?:Nơi\s+cấp|Sở\s+GTVT|So\s+GTVT)[:\s]*(.+?)(?=Ngày|\n|$)", re.IGNORECASE)


class LicenseOcrService:
    _instance: LicenseOcrService | None = None
    _lock = threading.Lock()

    def __init__(self) -> None:
        from paddleocr import PaddleOCR
        self._ocr = PaddleOCR(use_textline_orientation=True, lang="vi")
        logger.info("license_ocr_initialized")

    @classmethod
    def get_instance(cls) -> LicenseOcrService:
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

    def _extract(self, pattern: re.Pattern, text: str) -> str:
        m = pattern.search(text)
        return m.group(1).strip() if m else ""

    def _confidence_from_fields(self, fields: dict[str, str]) -> float:
        critical = ["license_number", "license_grade", "expiry_date", "full_name"]
        filled = sum(1 for f in critical if fields.get(f))
        return round(filled / len(critical), 2)

    async def extract(self, front_image, back_image) -> LicenseExtraction:
        import asyncio
        from functools import partial

        loop = asyncio.get_event_loop()
        front_text = await loop.run_in_executor(None, partial(self._run_ocr, front_image))
        back_text = await loop.run_in_executor(None, partial(self._run_ocr, back_image))
        full_text = front_text + "\n" + back_text

        license_number = self._extract(_RE_LICENSE_NUM, full_text)
        full_name = self._extract(_RE_NAME, full_text)
        dob_raw = self._extract(_RE_DOB, full_text)
        address_raw = self._extract(_RE_ADDRESS, full_text)
        grade = self._extract(_RE_GRADE, full_text).upper()
        issue_raw = self._extract(_RE_ISSUE, full_text)
        expiry_raw = self._extract(_RE_EXPIRY, full_text)
        authority = self._extract(_RE_AUTHORITY, full_text)

        dob = parse_vietnamese_date(dob_raw) or ""
        issue_date = parse_vietnamese_date(issue_raw) or ""
        expiry = parse_vietnamese_date(expiry_raw) or ""

        fields = {
            "license_number": license_number,
            "license_grade": grade,
            "expiry_date": expiry,
            "full_name": full_name,
        }
        confidence = self._confidence_from_fields(fields)

        logger.info("license_extracted", grade=grade, confidence=confidence)

        return LicenseExtraction(
            license_number=license_number,
            full_name=full_name,
            date_of_birth=dob,
            address=normalize_address(address_raw),
            license_grade=grade,
            issue_date=issue_date,
            expiry_date=expiry,
            issuing_authority=authority.strip(),
            confidence=confidence,
            raw_text=full_text,
            suggested_form_values=LicenseSuggestedValues(
                license_number=license_number,
                license_grade=grade,
                license_expiry_date=expiry,
            ) if license_number else None,
        )
