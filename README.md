# LockProvider
A gRPC server to provide FIFO named locks

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
      "name": "test",
	  "owner": "test",
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