const { promisify } = require('util')
const { credentials, loadPackageDefinition } = require('@grpc/grpc-js')
const protoLoader = require('@grpc/proto-loader')

const packageDef = protoLoader.loadSync('../../../LockProviderApi/Protos/lock-provider.proto', {})
const packageObj = loadPackageDefinition(packageDef)

const client = new packageObj.LockProvider.LockProvider('localhost:5000', credentials.createInsecure())
const acquireAsync = promisify(client.Acquire.bind(client));
const releaseAsync = promisify(client.Release.bind(client));

(async function main() {
    let count = 0;
    let lastLog = Date.now();

	console.log('Starting gRPC stress test');
    while(true) {
        const lockName = crypto.randomUUID();
        const acquireReq = {
            owner: "StressTest",
            name: lockName,
            timeout: 10,
            timeToLive: 10
        };
        const acquireRes = await acquireAsync(acquireReq);
        if (acquireRes.result !== "True") {
            console.log(`Failed to acquire lock ${lockName}`);
        }

        const releaseReq = {
            owner: "StressTest",
            name: lockName,
        };
        const releaseRes = await releaseAsync(releaseReq);
        if (releaseRes.result !== "True") {
            console.log(`Failed to release lock ${lockName}`);
        }

        count++;
        var elapsed = Date.now() - lastLog;
        if (elapsed >= 1000) {
            let perSec = count / elapsed * 1000;
            console.log(`Locks per second: ${Math.round(perSec)}`);
            lastLog = Date.now();
            count = 0;
        }
    }
})().catch(err => {
    console.error('Fatal error:', err)
    process.exit(1)
})
