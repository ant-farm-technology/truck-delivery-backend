const EARTH_RADIUS_KM: f64 = 6371.0;

/// Straight-line distance in kilometres between two WGS-84 coordinates.
pub fn distance_km(lat1: f64, lng1: f64, lat2: f64, lng2: f64) -> f64 {
    let dlat = (lat2 - lat1).to_radians();
    let dlng = (lng2 - lng1).to_radians();
    let a = (dlat / 2.0).sin().powi(2)
        + lat1.to_radians().cos() * lat2.to_radians().cos() * (dlng / 2.0).sin().powi(2);
    let c = 2.0 * a.sqrt().asin();
    EARTH_RADIUS_KM * c
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn same_point_is_zero() {
        assert_eq!(distance_km(10.0, 106.0, 10.0, 106.0), 0.0);
    }

    #[test]
    fn ho_chi_minh_to_hanoi_approx_1500km() {
        let d = distance_km(10.8231, 106.6297, 21.0278, 105.8342);
        assert!((d - 1137.0).abs() < 50.0, "distance was {d}");
    }
}
