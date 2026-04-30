"""Unit tests for LicenseOcrService — mocks PaddleOCR."""

import pytest
from unittest.mock import MagicMock, patch
import numpy as np

from ocr.services.license_ocr import LicenseOcrService

SAMPLE_LICENSE_TEXT = """
Giấy phép lái xe
Số: 079123456789
Họ và tên: NGUYEN VAN A
Ngày sinh: 15/05/1990
Nơi cư trú: 123 Nguyễn Trãi, HCM
Hạng: C
Ngày cấp: 01/01/2020
Ngày hết hạn: 31/12/2028
Nơi cấp: Sở GTVT TP.HCM
"""


@pytest.fixture
def mock_license_service():
    with patch("ocr.services.license_ocr.PaddleOCR") as MockPaddle:
        LicenseOcrService._instance = None

        mock_paddle = MagicMock()
        MockPaddle.return_value = mock_paddle

        lines = [(None, (line, 0.99)) for line in SAMPLE_LICENSE_TEXT.strip().split("\n") if line.strip()]
        mock_paddle.ocr.return_value = [lines]

        svc = LicenseOcrService.get_instance()
        yield svc

        LicenseOcrService._instance = None


@pytest.mark.asyncio
async def test_extract_license_number(mock_license_service):
    dummy = np.zeros((100, 100, 3), dtype=np.uint8)
    result = await mock_license_service.extract(dummy, dummy)
    assert result.license_number == "079123456789"


@pytest.mark.asyncio
async def test_extract_license_grade(mock_license_service):
    dummy = np.zeros((100, 100, 3), dtype=np.uint8)
    result = await mock_license_service.extract(dummy, dummy)
    assert result.license_grade == "C"


@pytest.mark.asyncio
async def test_extract_expiry_date(mock_license_service):
    dummy = np.zeros((100, 100, 3), dtype=np.uint8)
    result = await mock_license_service.extract(dummy, dummy)
    assert result.expiry_date == "2028-12-31"


@pytest.mark.asyncio
async def test_suggested_values_populated(mock_license_service):
    dummy = np.zeros((100, 100, 3), dtype=np.uint8)
    result = await mock_license_service.extract(dummy, dummy)
    assert result.suggested_form_values is not None
    assert result.suggested_form_values.license_grade == "C"
