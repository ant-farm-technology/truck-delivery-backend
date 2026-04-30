"""Giấy đăng ký xe OCR extraction service."""

from __future__ import annotations

import re
import threading

import structlog

from ocr.models.response import VehicleRegistrationExtraction, VehicleRegSuggestedValues
from ocr.services.name_normalizer import normalize_vietnamese_name, parse_vietnamese_date

logger = structlog.get_logger(__name__)

# Vietnamese license plate: "51C-12345" or "51C1-1234" or new format "51K1-12345"
_RE_PLATE = re.compile(r"\b(\d{2}[A-Z]\d?[-–]\d{4,5})\b")
_RE_BRAND = re.compile(r"(?:Nhãn\s+hiệu|Nhan\s+hieu)[:\s]+(\S+(?:\s+\S+)?)", re.IGNORECASE)
_RE_MODEL = re.compile(r"(?:Số\s+loại|Model|So\s+loai)[:\s]+(\S+)", re.IGNORECASE)
_RE_YEAR = re.compile(r"(?:Năm\s+sản\s+xuất|Nam\s+san\s+xuat)[:\s]+(\d{4})", re.IGNORECASE)
_RE_CHASSIS = re.compile(r"(?:Số\s+khung|So\s+khung)[:\s]+([A-Z0-9]+)", re.IGNORECASE)
_RE_ENGINE = re.compile(r"(?:Số\s+máy|So\s+may)[:\s]+([A-Z0-9]+)", re.IGNORECASE)
_RE_REG_NUM = re.compile(r"(?:Số\s+đăng\s+ký|So\s+dang\s+ky)[:\s]+(\S+)", re.IGNORECASE)
_RE_OWNER = re.compile(r"(?:Tên\s+chủ\s+xe|Ten\s+chu\s+xe|Chủ\s+xe)[:\s]+(.+?)(?=Địa|CCCD|Số|$)", re.IGNORECASE | re.DOTALL)
_RE_OWNER_ID = re.compile(r"(?:CCCD|CMND|Số\s+CMND|So\s+CCCD)[:\s]+(\d{9}|\d{12})", re.IGNORECASE)
_RE_EXPIRY = re.compile(r"(?:Hết\s+hạn|Het\s+han|Có\s+giá\s+trị\s+đến)[:\s]+(\d{2}[/-]\d{2}[/-]\d{4})", re.IGNORECASE)


class VehicleRegistrationOcrService:
    _instance: VehicleRegistrationOcrService | None = None
    _lock = threading.Lock()

    def __init__(self) -> None:
        from paddleocr import PaddleOCR
        self._ocr = PaddleOCR(use_angle_cls=True, lang="vi", show_log=False)
        logger.info("vehicle_reg_ocr_initialized")

    @classmethod
    def get_instance(cls) -> VehicleRegistrationOcrService:
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
        critical = ["license_plate", "chassis_number", "engine_number", "registration_number"]
        filled = sum(1 for f in critical if fields.get(f))
        return round(filled / len(critical), 2)

    async def extract(self, front_image, back_image) -> VehicleRegistrationExtraction:
        import asyncio
        from functools import partial

        loop = asyncio.get_event_loop()
        front_text = await loop.run_in_executor(None, partial(self._run_ocr, front_image))
        back_text = await loop.run_in_executor(None, partial(self._run_ocr, back_image))
        full_text = front_text + "\n" + back_text

        plate = self._extract(_RE_PLATE, full_text)
        brand = self._extract(_RE_BRAND, full_text)
        model = self._extract(_RE_MODEL, full_text)
        year_str = self._extract(_RE_YEAR, full_text)
        chassis = self._extract(_RE_CHASSIS, full_text)
        engine = self._extract(_RE_ENGINE, full_text)
        reg_number = self._extract(_RE_REG_NUM, full_text)
        owner = self._extract(_RE_OWNER, full_text)
        owner_id = self._extract(_RE_OWNER_ID, full_text)
        expiry_raw = self._extract(_RE_EXPIRY, full_text)

        year = int(year_str) if year_str.isdigit() else None
        expiry = parse_vietnamese_date(expiry_raw) or ""

        fields = {
            "license_plate": plate,
            "chassis_number": chassis,
            "engine_number": engine,
            "registration_number": reg_number,
        }
        confidence = self._confidence_from_fields(fields)

        logger.info("vehicle_reg_extracted", plate=plate, confidence=confidence)

        return VehicleRegistrationExtraction(
            license_plate=plate,
            brand=brand,
            model=model,
            year_of_manufacture=year,
            chassis_number=chassis,
            engine_number=engine,
            registration_number=reg_number,
            owner_name=owner.strip(),
            owner_id_number=owner_id,
            expiry_date=expiry,
            confidence=confidence,
            raw_text=full_text,
            suggested_form_values=VehicleRegSuggestedValues(
                license_plate=plate,
                registration_number=reg_number,
            ) if plate else None,
        )
