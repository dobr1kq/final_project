import http from 'k6/http';
import { check, sleep } from 'k6';

export let options = {
    stages: [
        { duration: '30s', target: 20 },
        { duration: '1m', target: 20 },
        { duration: '10s', target: 0 },
    ],
    thresholds: {
        http_req_duration: ['p(95)<500'],
    },
};

export default function () {
    let res = http.get('http://localhost:5000/api/products');
    check(res, { 'status is 200': (r) => r.status === 200 });

    let params = { headers: { 'X-User-Id': `user-${__VU}` } };
    let checkoutRes = http.post('http://localhost:5000/api/cart/checkout', null, params);
    
    check(checkoutRes, {
        'checkout success or validation fail': (r) => r.status === 200 || r.status === 400,
    });

    sleep(1);
}