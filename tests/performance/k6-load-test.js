// ==============================================================================
// CBS Bangladesh Core Banking System - High-Throughput k6 Performance Test Suite
// Conforming to Bangladesh Bank BRPD Circular No. 12 (13 May 2024)
// ==============================================================================

import http from 'k6/http';
import { check, group, sleep } from 'k6';
import { Rate, Trend } from 'k6/metrics';

// Custom Metrics
export const failureRate = new Rate('cbs_failure_rate');
export const healthDuration = new Trend('cbs_health_duration');
export const remittanceDuration = new Trend('cbs_remittance_duration');
export const shariahDuration = new Trend('cbs_shariah_duration');

// Workload Profile & Performance Budgets
export const options = {
  stages: [
    { duration: '10s', target: 20 },  // Warm-up ramp
    { duration: '20s', target: 50 },  // Standard branch business hours peak
    { duration: '15s', target: 100 }, // EOD Clearing & Remittance festival surge
    { duration: '10s', target: 0 },   // Cool-down
  ],
  thresholds: {
    'http_req_duration': ['p(95)<120', 'p(99)<250'], // 95% of transactions under 120ms
    'http_req_failed': ['rate<0.01'],               // Less than 1% HTTP failures
    'cbs_failure_rate': ['rate<0.005'],             // Banking transactions must achieve >99.5% success
  },
};

const BASE_URL = __ENV.TARGET_URL || 'http://localhost:5000';

export default function () {
  const headers = {
    'Content-Type': 'application/json',
    'X-User-Id': 'PERF_MAKER_01',
    'X-Branch-Code': '001',
  };

  // 1. Health & Liveness Probe
  group('Liveness & Readiness Probes', function () {
    const res = http.get(`${BASE_URL}/health`);
    const isHealthy = check(res, {
      'health status is 200': (r) => r.status === 200,
      'health response is Healthy': (r) => JSON.parse(r.body).status === 'Healthy',
    });
    failureRate.add(!isHealthy);
    healthDuration.add(res.timings.duration);
  });

  // 2. Foreign Remittance 2.5% Cash Incentive Calculation
  group('Foreign Remittance Cash Incentive', function () {
    const payload = JSON.stringify({
      amount: 150000.00,
      beneficiaryNid: '19901234567890123',
      purpose: 'FamilyMaintenance',
      exchangeHouseCode: 'EXCH-UK-01',
    });

    const res = http.post(`${BASE_URL}/api/v1/remittance/calculate-incentive`, payload, { headers });
    const isSuccess = check(res, {
      'remittance calculation status is 200': (r) => r.status === 200,
      'incentive amount matches 2.5%': (r) => {
        try {
          const body = JSON.parse(r.body);
          return body.incentiveAmount === 3750.00;
        } catch (e) {
          return false;
        }
      },
    });
    failureRate.add(!isSuccess);
    remittanceDuration.add(res.timings.duration);
  });

  // 3. Shariah Mudaraba Investment Pools Query
  group('Islamic Banking Mudaraba Pools', function () {
    const res = http.get(`${BASE_URL}/api/v1/shariah/pools`, { headers });
    const isSuccess = check(res, {
      'shariah pools status is 200': (r) => r.status === 200,
    });
    failureRate.add(!isSuccess);
    shariahDuration.add(res.timings.duration);
  });

  sleep(0.5); // Inter-transaction pacing
}
