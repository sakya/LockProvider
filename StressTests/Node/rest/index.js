import fetch from 'node-fetch';

async function stressRest(index) {
    let count = 0;
    let lastLog = Date.now();

	console.log('Starting REST stress test');
    while (true) {
        const lockName = crypto.randomUUID();
        const requestBody = {
            Owner: 'StressTest',
            Name: lockName,
            Timeout: 10,
            TimeToLive: 10
        };

        const acquireRes = await fetch('http://localhost:5001/acquire', {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json'
            },
            body: JSON.stringify(requestBody)
        });

        const acquireResult = await acquireRes.json();
        if (!acquireResult?.result) {
            console.log(`Failed to acquire lock ${lockName}`);
        }

        const releaseRes = await fetch(`http://localhost:5001/release?owner=StressTest&name=${lockName}`, {
            method: 'DELETE'
        });

        const releaseResult = await releaseRes.json();
        if (!releaseResult?.result) {
            console.log(`Failed to release lock ${lockName}`);
        }

        count++;

        const elapsed = Date.now() - lastLog;
        if (elapsed >= 1000) {
            const perSec = (count / elapsed) * 1000;
            console.log(`[${index}] Locks per second: ${Math.round(perSec).toLocaleString()}`);
            lastLog = Date.now();
            count = 0;
        }
    }
}

(async function main() {
    await stressRest(1);
})().catch(err => {
    console.error('Fatal error:', err)
    process.exit(1)
})
