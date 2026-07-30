import http from 'k6/http';
import { check, sleep } from 'k6';
import { Rate, Trend } from 'k6/metrics';

const errorRate = new Rate('errors');
const irmeLatency = new Trend('irme_calculation_latency');

export const options = {
  stages: [
    { duration: '2m', target: 50 },
    { duration: '5m', target: 50 },
    { duration: '2m', target: 0 },
  ],
  thresholds: {
    errors: ['rate<0.01'],
    http_req_duration: ['p(95)<2000'],
    irme_calculation_latency: ['p(95)<500'],
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

  const passed = check(res, {
    'status is 200': (r) => r.status === 200,
    'has lecturaId': (r) => r.json('lecturaId') !== undefined,
    'has nivelRiesgo': (r) => r.json('nivelRiesgo') !== undefined,
    'has message': (r) => r.json('message') === 'Lectura recibida',
  });

  errorRate.add(!passed);

  if (res.status === 200 && res.json('probabilidadPico') !== undefined) {
    irmeLatency.add(res.timings.duration);
  }

  sleep(Math.random() * 20 + 10);
}
