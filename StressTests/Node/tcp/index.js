const net = require('net');
const { promisify } = require('util');

async function stressTcp(index) {
    let count = 0;
    let lastLog = Date.now();

    const socket = new net.Socket();
    socket.setKeepAlive(true);

    const connectAsync = promisify(socket.connect).bind(socket);
    try {
        await connectAsync(5002, 'localhost');
    } catch (error) {
        console.error(`Connection error: ${error.message}`);
        return false;
    }

    const sendReceive = async (message) => {
        return new Promise((resolve, reject) => {
            socket.write(message, (err) => {
                if (err) {
                    reject(err);
                } else {
                    socket.once('data', (data) => {
                        resolve(data.toString());
                    });
                }
            });
        });
    };

    while (true) {
        const commandId = `${index}-${generateGuid()}`;
        const lockName = generateGuid();

        try {
            const acquireMessage = `ACQUIRE;Id=${commandId};Owner=StressTest;Name=${lockName};Timeout=10;TimeToLive=10;\n`;
            const acquireResponse = await sendReceive(acquireMessage);
            const acquireResult = parseTcpResponse(acquireResponse);

            if (acquireResult.Result !== "True" || acquireResult.Id !== commandId) {
                console.log(`Failed to acquire lock ${lockName}`);
            }

            const releaseMessage = `RELEASE;Id=${commandId};Owner=StressTest;Name=${lockName};\n`;
            const releaseResponse = await sendReceive(releaseMessage);
            const releaseResult = parseTcpResponse(releaseResponse);

            if (releaseResult.Result !== "True" || releaseResult.Id !== commandId) {
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

        } catch (error) {
            console.error(`Error: ${error.message}`);
        }
    }
}

function generateGuid() {
    return 'xxxxxxxx-xxxx-4xxx-yxxx-xxxxxxxxxxxx'.replace(/[xy]/g, function (c) {
        const r = Math.random() * 16 | 0;
        const v = c === 'x' ? r : (r & 0x3 | 0x8);
        return v.toString(16);
    });
}

function parseTcpResponse(response) {
    const result = {};
    const pairs = response.split(';');
    pairs.forEach(pair => {
        const [key, value] = pair.split('=');
        if (key && value) {
            result[key.trim()] = value.trim();
        }
    });
    return result;
}

(async function main() {
    await stressTcp(1);
})().catch(err => {
    console.error('Fatal error:', err)
    process.exit(1)
})