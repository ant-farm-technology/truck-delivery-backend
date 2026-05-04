/** Shared request payload builders */

export function createOrderPayload() {
  return {
    pickupAddress: {
      street: '123 Nguyen Hue',
      city: 'Ho Chi Minh',
      province: 'Ho Chi Minh',
      postalCode: '70000',
      countryCode: 'VN',
      latitude: 10.7769 + (Math.random() - 0.5) * 0.1,
      longitude: 106.7009 + (Math.random() - 0.5) * 0.1,
    },
    deliveryAddress: {
      street: '456 Le Loi',
      city: 'Ha Noi',
      province: 'Ha Noi',
      postalCode: '10000',
      countryCode: 'VN',
      latitude: 21.0285 + (Math.random() - 0.5) * 0.1,
      longitude: 105.8542 + (Math.random() - 0.5) * 0.1,
    },
    items: [
      {
        productName: 'Load Test Cargo',
        quantity: 1,
        weightKg: 10.0 + Math.random() * 40,
        volumeCbm: 0.1 + Math.random() * 0.5,
      },
    ],
  };
}

export function locationUpdatePayload(shipmentId, driverId) {
  return {
    shipmentId,
    driverId,
    latitude: 10.7769 + (Math.random() - 0.5) * 0.5,
    longitude: 106.7009 + (Math.random() - 0.5) * 0.5,
    speedKmh: Math.random() * 80,
    heading: Math.random() * 360,
    timestamp: new Date().toISOString(),
  };
}
