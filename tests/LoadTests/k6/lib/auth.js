import http from 'k6/http';
import { check } from 'k6';

const BASE_URL = __ENV.GATEWAY_URL || 'http://localhost:8080';

/**
 * Registers a user and returns { userId, accessToken, refreshToken }.
 * Calls Identity service directly (no Gateway auth needed for register/login).
 */
export function registerAndLogin(emailPrefix, role = 1) {
  const suffix = `${Date.now()}_${Math.random().toString(36).slice(2, 7)}`;
  const email = `${emailPrefix}_${suffix}@loadtest.local`;
  const password = 'Load@1234';

  const registerResp = http.post(
    `${BASE_URL}/api/v1/auth/register`,
    JSON.stringify({
      firstName: 'Load',
      lastName: 'Test',
      email,
      password,
      phoneNumber: '0900000000',
      dateOfBirth: '1990-01-01',
      role,
    }),
    { headers: { 'Content-Type': 'application/json' } },
  );

  check(registerResp, { 'register 201': (r) => r.status === 201 });

  const loginResp = http.post(
    `${BASE_URL}/api/v1/auth/login`,
    JSON.stringify({ email, password }),
    { headers: { 'Content-Type': 'application/json' } },
  );

  check(loginResp, { 'login 200': (r) => r.status === 200 });

  const body = JSON.parse(loginResp.body);
  const data = body.data ?? body;
  return {
    userId: data.userId,
    accessToken: data.accessToken,
    refreshToken: data.refreshToken,
  };
}

export function bearerHeaders(token) {
  return {
    'Content-Type': 'application/json',
    Authorization: `Bearer ${token}`,
  };
}
