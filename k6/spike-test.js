import http from 'k6/http';
import { check, sleep } from 'k6';

export const options = {
  stages: [
    { duration: '30s', target: 10 },
    { duration: '10s', target: 300 },
    { duration: '30s', target: 300 },
    { duration: '30s', target: 10 },
    { duration: '1m', target: 0 },
  ],
  thresholds: {
    http_req_duration: ['p(95)<5000'],
    http_req_failed: ['rate<0.10'],
  },
};

const BASE_URL = __ENV.BASE_URL || 'http://localhost:5000';
const PACIENTE_TOKEN = __ENV.PACIENTE_TOKEN || '';

const BATCH_SIZE = 10;

function generateBatch() {
  const batch = [];
  for (let i = 0; i < BATCH_SIZE; i++) {
    batch.push({
      pulsoBpm: Math.floor(Math.random() * 40) + 55,
      temperaturaC: Math.random() * 3 + 35,
      sudoracionGsr: Math.random() * 8,
      hrv: Math.floor(Math.random() * 60) + 20,
      spo2: Math.floor(Math.random() * 5) + 95,
    });
  }
  return batch;
}

export default function () {
  const endpoint = Math.random() < 0.5 ? 'lectura-batch' : 'tracking-batch';

  const body = endpoint === 'lectura-batch'
    ? JSON.stringify(generateBatch())
    : JSON.stringify(
        generateBatch().map((r) => ({
          latitud: Math.random() * 180 - 90,
          longitud: Math.random() * 360 - 180,
          timestamp: new Date().toISOString(),
        }))
      );

  const res = http.post(`${BASE_URL}/api/Sensores/${endpoint}`, body, {
    headers: {
      'Content-Type': 'application/json',
      Authorization: `Bearer ${PACIENTE_TOKEN}`,
    },
  });

  check(res, {
    'status is 200': (r) => r.status === 200,
    'has message': (r) => r.json('message') !== undefined,
  });

  sleep(Math.random() * 1 + 0.5);
}
