"""Unit tests for VehicleRegistrationOcrService — mocks PaddleOCR."""

import pytest
from unittest.mock import MagicMock, patch
import numpy as np

from ocr.services.vehicle_reg_ocr import VehicleRegistrationOcrService

SAMPLE_VEHICLE_REG_TEXT = """
Giấy chứng nhận đăng ký xe
Biển số: 51C-12345
Nhãn hiệu: HINO
Số loại: XZU720L
Năm sản xuất: 2020
Số khung: JHDFF1JK1NX000123
Số máy: A05C0000456
Số đăng ký: HCM-20-1234
Tên chủ xe: NGUYEN VAN A
CCCD: 079123456789
Hết hạn: 31/12/2025
"""


@pytest.fixture
def mock_vehicle_service():
    with patch("ocr.services.vehicle_reg_ocr.PaddleOCR") as MockPaddle:
        VehicleRegistrationOcrService._instance = None

        mock_paddle = MagicMock()
        MockPaddle.return_value = mock_paddle

        lines = [(None, (line, 0.99)) for line in SAMPLE_VEHICLE_REG_TEXT.strip().split("\n") if line.strip()]
        mock_paddle.ocr.return_value = [lines]

        svc = VehicleRegistrationOcrService.get_instance()
        yield svc

        VehicleRegistrationOcrService._instance = None


@pytest.mark.asyncio
async def test_extract_license_plate(mock_vehicle_service):
    dummy = np.zeros((100, 100, 3), dtype=np.uint8)
    result = await mock_vehicle_service.extract(dummy, dummy)
    assert result.license_plate == "51C-12345"


@pytest.mark.asyncio
async def test_extract_brand(mock_vehicle_service):
    dummy = np.zeros((100, 100, 3), dtype=np.uint8)
    result = await mock_vehicle_service.extract(dummy, dummy)
    assert result.brand == "HINO"


@pytest.mark.asyncio
async def test_extract_year(mock_vehicle_service):
    dummy = np.zeros((100, 100, 3), dtype=np.uint8)
    result = await mock_vehicle_service.extract(dummy, dummy)
    assert result.year_of_manufacture == 2020


@pytest.mark.asyncio
async def test_extract_owner_id(mock_vehicle_service):
    dummy = np.zeros((100, 100, 3), dtype=np.uint8)
    result = await mock_vehicle_service.extract(dummy, dummy)
    assert result.owner_id_number == "079123456789"


@pytest.mark.asyncio
async def test_suggested_values_populated(mock_vehicle_service):
    dummy = np.zeros((100, 100, 3), dtype=np.uint8)
    result = await mock_vehicle_service.extract(dummy, dummy)
    assert result.suggested_form_values is not None
    assert result.suggested_form_values.license_plate == "51C-12345"
