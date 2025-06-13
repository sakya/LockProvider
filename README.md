# LockProvider
A gRPC server to provide FIFO named locks

## Methods
### Status
Get the status of the server.

Proto file: [here](https://github.com/sakya/LockProvider/blob/main/LockProviderApi/Protos/lock.proto)

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
### LocksList
Get a list of acquired locks

Request:
```json
{}
```

Response:
```json
{
  "locks": [
    {
      "owner": "test",
      "name": "test",
      "acquiredAt": "2025-06-13T16:41:46.0393747Z"
    }
  ]
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
  "name": "test"
}
```

Response:
```json
{
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
  "name": "test"
}
```

Response:
```json
{
  "name": "test",
  "result": "True",
  "timeStamp": "2025-06-13T16:45:20.4941284Z"
}
```

If the lock cannot be released (i.e. the lock was not found)
```json
{
  "name": "test",
  "result": "False",
  "error": "NotFound",
  "timeStamp": "2025-06-13T17:06:55.1440345Z"
}
```