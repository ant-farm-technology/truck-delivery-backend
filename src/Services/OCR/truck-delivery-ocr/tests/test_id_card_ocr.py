"""Unit tests for IdCardOcrService — mocks PaddleOCR to avoid model dependency."""

import pytest
from unittest.mock import MagicMock, patch
import numpy as np

from ocr.services.id_card_ocr import IdCardOcrService


SAMPLE_CCCD_TEXT = """
Cộng hòa xã hội chủ nghĩa Việt Nam
Căn cước công dân
Số: 079123456789
Họ và tên: NGUYEN VAN A
Ngày sinh: 15/05/1990
Giới tính: Nam
Quốc tịch: Việt Nam
Quê quán: Hà Nội
Nơi thường trú: 123 Nguyễn Trãi, Quận 1, TP.HCM
Có giá trị đến: 15/05/2035
"""


@pytest.fixture
def mock_ocr_service():
    """IdCardOcrService with mocked PaddleOCR."""
    with patch("ocr.services.id_card_ocr.PaddleOCR") as MockPaddle:
        # Reset singleton
        IdCardOcrService._instance = None

        mock_paddle = MagicMock()
        MockPaddle.return_value = mock_paddle

        # Simulate OCR returning lines from SAMPLE_CCCD_TEXT
        lines = [(None, (line, 0.99)) for line in SAMPLE_CCCD_TEXT.strip().split("\n") if line.strip()]
        mock_paddle.ocr.return_value = [lines]

        svc = IdCardOcrService.get_instance()
        yield svc

        # Reset singleton after test
        IdCardOcrService._instance = None


@pytest.mark.asyncio
async def test_extract_id_number(mock_ocr_service):
    dummy = np.zeros((100, 100, 3), dtype=np.uint8)
    result = await mock_ocr_service.extract(dummy, dummy)
    assert result.id_number == "079123456789"


@pytest.mark.asyncio
async def test_extract_date_of_birth(mock_ocr_service):
    dummy = np.zeros((100, 100, 3), dtype=np.uint8)
    result = await mock_ocr_service.extract(dummy, dummy)
    assert result.date_of_birth == "1990-05-15"


@pytest.mark.asyncio
async def test_extract_expiry(mock_ocr_service):
    dummy = np.zeros((100, 100, 3), dtype=np.uint8)
    result = await mock_ocr_service.extract(dummy, dummy)
    assert result.expiry_date == "2035-05-15"


@pytest.mark.asyncio
async def test_extract_gender(mock_ocr_service):
    dummy = np.zeros((100, 100, 3), dtype=np.uint8)
    result = await mock_ocr_service.extract(dummy, dummy)
    assert result.gender == "Nam"


@pytest.mark.asyncio
async def test_confidence_not_zero_when_fields_found(mock_ocr_service):
    dummy = np.zeros((100, 100, 3), dtype=np.uint8)
    result = await mock_ocr_service.extract(dummy, dummy)
    assert result.confidence > 0.0


@pytest.mark.asyncio
async def test_suggested_form_values_populated(mock_ocr_service):
    dummy = np.zeros((100, 100, 3), dtype=np.uint8)
    result = await mock_ocr_service.extract(dummy, dummy)
    assert result.suggested_form_values is not None
    assert result.suggested_form_values.date_of_birth == "1990-05-15"
