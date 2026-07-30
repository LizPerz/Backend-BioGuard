import http from 'k6/http';
import { check, sleep } from 'k6';

export const options = {
  stages: [
    { duration: '2m', target: 20 },
    { duration: '3m', target: 50 },
    { duration: '3m', target: 100 },
    { duration: '3m', target: 200 },
    { duration: '3m', target: 400 },
    { duration: '5m', target: 400 },
    { duration: '2m', target: 0 },
  ],
  thresholds: {
    http_req_duration: ['p(90)<3000'],
    http_req_failed: ['rate<0.05'],
  },
};

const BASE_URL = __ENV.BASE_URL || 'http://localhost:5000';
const PACIENTE_TOKEN = __ENV.PACIENTE_TOKEN || '';

const PAYLOAD = {
  pulsoBpm: 72,
  temperaturaC: 36.5,
  sudoracionGsr: 3.2,
  hrv: 45,
  spo2: 98,
};

export default function () {
  const res = http.post(
    `${BASE_URL}/api/Sensores/lectura`,
    JSON.stringify(PAYLOAD),
    {
      headers: {
        'Content-Type': 'application/json',
        Authorization: `Bearer ${PACIENTE_TOKEN}`,
      },
    }
  );

  check(res, {
    'status is 200 or 429': (r) => r.status === 200 || r.status === 429,
  });

  if (res.status === 429) {
    sleep(2);
  } else {
    sleep(Math.random() * 20 + 10);
  }
}
