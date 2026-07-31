import http from 'k6/http';
import { check } from 'k6';

export const options = {
  vus: 1,
  iterations: 3,
  thresholds: {
    http_req_duration: ['p(95)<3000'],
    http_req_failed: ['rate<0.01'],
  },
};

const BASE_URL = __ENV.BASE_URL || 'http://localhost:5000';
const TEST_EMAIL = __ENV.TEST_EMAIL || 'test@bioguard.test';
const TEST_PASSWORD = __ENV.TEST_PASSWORD || 'Test@123!';
const TEST_PACIENTE_TOKEN = __ENV.PACIENTE_TOKEN || '';

export default function () {
  const healthRes = http.get(`${BASE_URL}/health`);
  check(healthRes, {
    'health: status is 200': (r) => r.status === 200,
    'health: body has status healthy': (r) => r.json('status') === 'healthy',
    'health: body has timestamp': (r) => r.json('timestamp') !== undefined,
  });

  const loginRes = http.post(
    `${BASE_URL}/api/Auth/login-web`,
    JSON.stringify({ Correo: TEST_EMAIL, Password: TEST_PASSWORD }),
    { headers: { 'Content-Type': 'application/json' } }
  );
  check(loginRes, {
    'login: status is 200': (r) => r.status === 200,
    'login: body has token': (r) => r.json('token') !== undefined,
  });

  if (TEST_PACIENTE_TOKEN) {
    const lecturaRes = http.post(
      `${BASE_URL}/api/Sensores/lectura`,
      JSON.stringify({ pulsoBpm: 72, temperaturaC: 36.5, sudoracionGsr: 3.2, hrv: 45, spo2: 98 }),
      {
        headers: {
          'Content-Type': 'application/json',
          Authorization: `Bearer ${TEST_PACIENTE_TOKEN}`,
        },
      }
    );
    check(lecturaRes, {
      'lectura: status is 200': (r) => r.status === 200,
      'lectura: body has lecturaId': (r) => r.json('lecturaId') !== undefined,
    });
  }
}
