# LockProvider
[![CodeFactor](https://www.codefactor.io/repository/github/sakya/lockprovider/badge)](https://www.codefactor.io/repository/github/sakya/lockprovider)
[![Docker Image Version](https://img.shields.io/docker/v/paoloiommarini/lock-provider?sort=semver)](https://hub.docker.com/r/paoloiommarini/lock-provider)

A gRPC, REST and TCP server to provide FIFO named locks

Default local ports: 
- gRPC: 5000
- REST: 5001
- TCP: 5002

Default docker exposed ports:
- gRPC: 5200
- REST: 5201
- TCP: 5202

## Run docker image
Pull the image
```shell
docker pull paoloiommarini/lock-provider:latest
```
Run the container
```shell
docker run --name LockProvider -p 5200:5000 -p 5201:5001 -p 5202:5002 -d --restart unless-stopped paoloiommarini/lock-provider
```

## gRPC methods
Proto file: [here](https://github.com/sakya/LockProvider/blob/main/LockProviderApi/Protos/lock-provider.proto)

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
Try to acquire a lock.\
Locks must be unique by owner and name, this means that different owners can acquire a lock with the same name

If `timeToLive` is greater than zero the lock will be automatically released after timeToLive seconds.

Request:
```json
{
  "owner": "test",
  "name": "test",
  "timeout": 5,
  "timeToLive": 10
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

## REST
Swagger is available [here](https://sakya.github.io/LockProvider/)


## TCP protocol
A command is composed by the command name followed by a number of arguments in the format `name=value` separated by a semicolon `;`

If an argument value contains the character `;` it must be escaped with a backslash `\;`

Each command must end with a newline character `\n`

Each command must have a `Id` argument. The `Id` is returned in the server response.

### Status
Command:

`STATUS;Id=s123;`

Response:
`Id=s123;Locks=0;Result=True;ServerVersion=1.3.0.0;TimeStamp=2025-12-28T09:48:11.0767468Z;Uptime=0:25:42.8594;WaitingLocks=0;`

### Acquire
Command:

`ACQUIRE;Id=123-456;Owner=lockOwner;Name=lockName;Timeout=10;TimeToLive=10;`

Response:

`Id=123-456;Name=lockName;Owner=lockOwner;Result=True;TimeStamp=2025-12-26T16:30:09.1702406Z;`

### Is locked
Command:

`ISLOCKED;Id=555-123;Owner=lockOwner;Name=lockName;`

Response:

`Id=555-123;Name=lockName;Owner=lockOwner;Result=True;TimeStamp=2025-12-26T16:30:09.1702406Z;`

### Release
Command:

`RELEASE;Id=789-012;Owner=lockOwner;Name=lockName;`

Response:

`Id=789-012;Name=lockName;Owner=lockOwner;Result=True;TimeStamp=2025-12-26T16:30:46.7478962Z;`

### Release many
Command:

`RELEASEMANY;Id=543-210;Owner=lockOwner;Name=*;`

Response:

`Id=543-210;Name=lockName;Owner=*;Result=True;Count=4;TimeStamp=2025-12-26T16:30:46.7478962Z;`

## Benchmarks
On my machine with these specs
```
System:
  Kernel: 6.18.2-arch2-1 arch: x86_64 bits: 64 compiler: gcc v: 15.2.1
  Desktop: GNOME v: 49.2 Distro: Arch Linux
CPU:
  Info: quad core model: Intel Core i7-4700HQ bits: 64 type: MT MCP
    arch: Haswell rev: 3 cache: L1: 256 KiB L2: 1024 KiB L3: 6 MiB
  Speed (MHz): avg: 800 min/max: 800/3400 cores: 1: 800 2: 800 3: 800 4: 800
    5: 800 6: 800 7: 800 8: 800 bogomips: 38313
  Flags-basic: avx avx2 ht lm nx pae sse sse2 sse3 sse4_1 sse4_2 ssse3 vmx
Info:
  Memory: total: 16 GiB note: est. available: 15.31 GiB used: 2.45 GiB (16.0%)
```

running LockProviderApi locally (no docker container) and using the provided [stress tests](https://github.com/sakya/LockProvider/tree/develop/StressTests), I get this results (single thread client)

| Technology |   gRPC |   REST |   TCP |
|:-----------|-------:|-------:|------:|
| dotnet     |   3400 |   3700 | 15000 |
| Node.js    |   1100 |   1000 |  9000 |

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
