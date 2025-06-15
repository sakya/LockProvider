# LockProvider
A gRPC server to provide FIFO named locks

Proto file: [here](https://github.com/sakya/LockProvider/blob/main/LockProviderApi/Protos/lock-provider.proto)

## Run docker image
Pull the image
```shell
docker pull paoloiommarini/lock-provider:latest
```
Run the container (in this example the server is reachable at `localhost:5200`)
```shell
docker run --name LockProvider -p 5200:5000 -d paoloiommarini/lock-provider
```

## Methods
### Status
Get the status of the server.

Request:
```json
{}
```

Response:
```json
{
  "serverVersion": "1.0.0.0",
  "uptime": "00:00:05.6849020",
  "locks": 1,
  "waitingLocks": 1,
  "timeStamp": "2025-06-13T16:42:51.8657848Z"
}
```
### List
Get a list of acquired locks.

The owner must match exactly.\
The name is a regex to filter locks

Request:
```json
{
  "owner": "test",
  "name": "*"  
}
```

Response:
```json
{
  "owner": "test",
  "name": "*",
  "count": 1,  
  "locks": [
    {
      "owner": "test",
      "name": "test",
      "acquiredAt": "2025-06-13T16:41:46.0393747Z"
    }
  ],
  "timeStamp": "2025-06-13T16:41:42.0393747Z"
}
```
### Acquire
Try to acquire a lock

Request:
```json
{
  "owner": "test",
  "name": "test",
  "timeout": 5
}
```

Response:
```json
{
  "owner": "test",
  "name": "test",
  "result": "True",
  "timeStamp": "2025-06-13T16:47:58.6890059Z"
}
```
If the lock cannot be acquired the `error` property contains the error message
```json
{
  "owner": "test",
  "name": "test",
  "result": "False",
  "error": "Timeout",
  "timeStamp": "2025-06-13T16:56:18.3054261Z"
}
```
### IsLocked
Check if a name is locked

Request:
```json
{
  "owner": "test",  
  "name": "test"
}
```

Response:
```json
{
  "owner": "test",  
  "name": "test",
  "result": "True",
  "timeStamp": "2025-06-13T16:50:00.7567875Z"
}
```

### Release
Release a lock

Request:
```json
{
  "owner": "test",  
  "name": "test"
}
```

Response:
```json
{
  "owner": "test",  
  "name": "test",
  "result": "True",
  "timeStamp": "2025-06-13T16:45:20.4941284Z"
}
```

If the lock cannot be released (i.e., the lock was not found)
```json
{
  "owner": "test",  
  "name": "test",
  "result": "False",
  "error": "NotFound",
  "timeStamp": "2025-06-13T17:06:55.1440345Z"
}
```

### ReleaseMany
Release multiple locks

The owner must match exactly.\
The name is a regex to filter locks

Request:
```json
{
  "owner": "test",  
  "name": "*"
}
```

Response:
```json
{
  "owner": "test",
  "name": "*",
  "count": 1,
  "locks": [
    {
      "owner": "test",
      "name": "lock 1",
      "acquiredAt": "2025-06-14T07:17:58.3147789Z"
    }
  ],
  "timeStamp": "2025-06-14T07:17:59.1440345Z"  
}
```

## Node.js quick start (TypeScript)
- Create a new Node project
  ```shell
  mkdir lock-provider-quickstart
  cd lock-provider-quickstart
  mkdir src
  pnpm init
  pnpm add -D typescript @types/node
  pnpm add --save @grpc/grpc-js @grpc/proto-loader @bufbuild/protobuf
  pnpm install --save-dev grpc-tools grpc_tools_node_protoc_ts ts-proto
  npx tsc --init
  ```

- Set the file `tsconfig.json` with this content
  ```json
  {
    "compilerOptions": {
      "target": "ES2020",
      "module": "commonjs",
      "outDir": "./dist",
      "rootDir": "./src",
      "strict": true,
      "esModuleInterop": true
    },
    "include": ["src"]
  }
  ```

- Edit the `scripts` section in the file `package.json`
  ```json
  "scripts": {
    "build": "tsc",
    "start": "node dist/index.js",
    "dev": "nodemon --watch src --exec ts-node src/index.ts"
  },
  ```
- Copy the [proto file](https://github.com/sakya/LockProvider/blob/main/LockProviderApi/Protos/lock-provider.proto) in the `lock-provider-quickstart` directory.
- Create the typed client
  ```shell
  npx protoc --plugin=protoc-gen-ts_proto=./node_modules/.bin/protoc-gen-ts_proto --ts_proto_out=./src --ts_proto_opt=outputServices=grpc-js -I ./ ./lock-provider.proto
  ```
- Create the file `src/index.ts` with this content
  ```typescript
  import { ChannelCredentials } from "@grpc/grpc-js";
  import { LockAcquireRequest, LockProviderClient, LockRequest, LockResponse, LocksListResponse } from "./lock-provider";

  const client = new LockProviderClient('localhost:5200', ChannelCredentials.createInsecure());

  function releaseMany(request: LockRequest): Promise<LocksListResponse> {
    return new Promise((resolve, reject) => {
      client.releaseMany(request, (err, response) => {
        if (err) {
          reject(err);
        } else {
          if (response.result !== 'True') {
            reject(response.error);
            return;
          }
          resolve(response);
        }
      });
    });
  }

  function acquireLock(request: LockAcquireRequest): Promise<LockResponse> {
    return new Promise((resolve, reject) => {
      client.acquire(request, (err, response) => {
        if (err) {
          console.error(`Lock ${request.name} acquire failed:`, err);
          reject(err);
        } else {
          if (response.result !== 'True') {
            console.error(`Lock ${request.name} acquire failed:`, response.error);
            reject(response.error);
            return;
          }

          console.error(`Lock ${request.name} acquired`);
          resolve(response);
        }
      });
    });
  }

  function releaseLock(request: LockRequest): Promise<LockResponse> {
    return new Promise((resolve, reject) => {
      client.release(request, (err, response) => {
        if (err) {
          console.error(`Lock ${request.name} release failed:`, err);
          reject(err);
        } else {
          if (response.result !== 'True') {
            console.error(`Lock ${request.name} release failed:`, response.error);
            reject(response.error);
            return;
          }

          console.error(`Lock ${request.name} released`);
          resolve(response);
        }
      });
    });
  }

  (async () => {
    const owner: string = 'lock_owner';
    try {
      // Acquire lock_1
      await acquireLock({
        owner,
        name: 'lock_1',
        timeout: 5
      });

      // Release lock_1
      await releaseLock({
        owner,
        name: 'lock_1'
      })

      // Acquire lock_2
      await acquireLock({
        owner,
        name: 'lock_2',
        timeout: 5
      });

      // Try to acquire lock_2. This will fail after 5 seconds
      await acquireLock({
        owner,
        name: 'lock_2',
        timeout: 5
      });
    } catch (err) {
      console.error('Exception:', err);
    } finally {
      // Release all lock for this owner
      const rsm = await releaseMany({
        owner,
        name: '*',
      });
      console.log(`Released ${rsm.count} locks`);
    }

    client.close();
    process.exit(0);
  })();
  ```
- Build the project
  ```shell
  pnpm build
  ```
- Run the project
  ```shell
  pnpm start
  ```
- Expected output
  ```shell
  Lock lock_1 acquired
  Lock lock_1 released
  Lock lock_2 acquired
  Lock lock_2 acquire failed: Timeout
  Exception: Timeout
  Released 1 locks
  ```