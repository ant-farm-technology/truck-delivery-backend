"""Unit tests for verification logic — no PaddleOCR required."""

import uuid
from datetime import date, timedelta

import pytest

from ocr.models.response import CCCDExtraction, LicenseExtraction, VehicleRegistrationExtraction
from ocr.services.verification import compute_overall_verification, _cross_checks, _fuzzy_score


def make_cccd(**kwargs) -> CCCDExtraction:
    defaults = dict(
        id_number="079123456789",
        full_name="NGUYEN VAN A",
        date_of_birth="1990-05-15",
        gender="Nam",
        nationality="Việt Nam",
        place_of_origin="Hà Nội",
        place_of_residence="123 Nguyễn Trãi, HCM",
        expiry_date="2035-05-15",
        confidence=0.95,
        raw_text="sample",
    )
    defaults.update(kwargs)
    return CCCDExtraction(**defaults)


def make_license(**kwargs) -> LicenseExtraction:
    expiry = (date.today() + timedelta(days=365)).isoformat()
    defaults = dict(
        license_number="079123456789",
        full_name="NGUYEN VAN A",
        date_of_birth="1990-05-15",
        address="123 HCM",
        license_grade="C",
        issue_date="2020-01-01",
        expiry_date=expiry,
        issuing_authority="Sở GTVT HCM",
        confidence=0.92,
        raw_text="sample",
    )
    defaults.update(kwargs)
    return LicenseExtraction(**defaults)


def make_vehicle_reg(**kwargs) -> VehicleRegistrationExtraction:
    expiry = (date.today() + timedelta(days=180)).isoformat()
    defaults = dict(
        license_plate="51C-12345",
        brand="HINO",
        model="XZU720L",
        year_of_manufacture=2020,
        chassis_number="ABC123",
        engine_number="ENG456",
        registration_number="REG789",
        owner_name="NGUYEN VAN A",
        owner_id_number="079123456789",
        expiry_date=expiry,
        confidence=0.88,
        raw_text="sample",
    )
    defaults.update(kwargs)
    return VehicleRegistrationExtraction(**defaults)


def make_submitted(**kwargs) -> dict:
    defaults = dict(
        full_name="NGUYEN VAN A",
        date_of_birth="1990-05-15",
        id_card_number="",
        license_number="079123456789",
        license_grade="C",
        license_expiry=(date.today() + timedelta(days=365)).isoformat(),
        license_plate="51C-12345",
        registration_number="REG789",
    )
    defaults.update(kwargs)
    return defaults


class TestFuzzyScore:
    def test_identical_strings(self):
        assert _fuzzy_score("NGUYEN VAN A", "NGUYEN VAN A") == 1.0

    def test_empty_string(self):
        assert _fuzzy_score("", "NGUYEN VAN A") == 0.0

    def test_partial_match(self):
        score = _fuzzy_score("NGUYEN VAN A", "NGUYEN VAN B")
        assert 0.7 < score < 1.0


class TestCrossChecks:
    def test_all_checks_pass(self):
        cccd = make_cccd()
        lic = make_license()
        vreg = make_vehicle_reg()
        passed, reasons = _cross_checks(cccd, lic, vreg)
        assert passed is True
        assert reasons == []

    def test_name_mismatch_fails(self):
        cccd = make_cccd(full_name="NGUYEN VAN A")
        lic = make_license(full_name="TRAN THI B")
        vreg = make_vehicle_reg()
        passed, reasons = _cross_checks(cccd, lic, vreg)
        assert passed is False
        assert any("Tên" in r for r in reasons)

    def test_expired_license_fails(self):
        expired = (date.today() - timedelta(days=1)).isoformat()
        cccd = make_cccd()
        lic = make_license(expiry_date=expired)
        vreg = make_vehicle_reg()
        passed, reasons = _cross_checks(cccd, lic, vreg)
        assert passed is False
        assert any("hết hạn" in r for r in reasons)

    def test_invalid_grade_fails(self):
        cccd = make_cccd()
        lic = make_license(license_grade="B1")
        vreg = make_vehicle_reg()
        passed, reasons = _cross_checks(cccd, lic, vreg)
        assert passed is False
        assert any("không đủ" in r for r in reasons)

    def test_owner_id_mismatch_fails(self):
        cccd = make_cccd(id_number="079123456789")
        lic = make_license()
        vreg = make_vehicle_reg(owner_id_number="111111111111")
        passed, reasons = _cross_checks(cccd, lic, vreg)
        assert passed is False
        assert any("đăng ký xe" in r for r in reasons)


class TestOverallVerification:
    def test_ocr_verified_when_all_match(self):
        driver_id = uuid.uuid4()
        result = compute_overall_verification(
            driver_id=driver_id,
            cccd=make_cccd(),
            license_data=make_license(),
            vehicle_reg=make_vehicle_reg(),
            submitted=make_submitted(),
        )
        assert result.status == "ocr_verified"
        assert result.overall_confidence >= 0.85
        assert result.cross_checks_passed is True

    def test_manual_review_when_low_confidence(self):
        driver_id = uuid.uuid4()
        cccd = make_cccd(id_number="000000000000")  # won't match submitted
        result = compute_overall_verification(
            driver_id=driver_id,
            cccd=cccd,
            license_data=make_license(full_name="TOTALLY DIFFERENT NAME"),
            vehicle_reg=make_vehicle_reg(),
            submitted=make_submitted(),
        )
        assert result.status in ("manual_review", "rejected")

    def test_rejected_when_license_expired(self):
        driver_id = uuid.uuid4()
        expired = (date.today() - timedelta(days=1)).isoformat()
        result = compute_overall_verification(
            driver_id=driver_id,
            cccd=make_cccd(),
            license_data=make_license(expiry_date=expired),
            vehicle_reg=make_vehicle_reg(),
            submitted=make_submitted(),
        )
        assert result.status == "rejected"
