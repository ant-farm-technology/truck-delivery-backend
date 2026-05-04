from pydantic import BaseModel, HttpUrl


class ExtractIdCardRequest(BaseModel):
    front_url: str
    back_url: str


class ExtractLicenseRequest(BaseModel):
    front_url: str
    back_url: str


class ExtractVehicleRegRequest(BaseModel):
    front_url: str
    back_url: str
