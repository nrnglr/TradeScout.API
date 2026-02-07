# Health Check Endpoint

## Overview
The TradeScout API now includes a health check endpoint at the root path (`/`) that provides basic information about the API status and available endpoints.

## Endpoint Details

### GET /
Returns basic information about the API status.

**URL**: `http://localhost:5000/`

**Method**: `GET`

**Auth Required**: No (Anonymous access)

**Response Example**:
```json
{
    "status": "ok",
    "message": "TradeScout API is running",
    "version": "1.0.0",
    "timestamp": "2026-02-07T17:52:36.411581Z",
    "endpoints": {
        "auth": "/api/auth",
        "scraper": "/api/scraper"
    }
}
```

## Usage

### Using cURL
```bash
curl http://localhost:5000/
```

### Using Browser
Simply navigate to: `http://localhost:5000/`

### Using JavaScript/React
```javascript
fetch('http://localhost:5000/')
  .then(response => response.json())
  .then(data => console.log('API Status:', data.status))
  .catch(error => console.error('API is down:', error));
```

## Response Fields

| Field | Type | Description |
|-------|------|-------------|
| `status` | string | Current API status ("ok" when running) |
| `message` | string | Human-readable status message |
| `version` | string | Current API version |
| `timestamp` | string | Current server time in UTC (ISO 8601 format) |
| `endpoints` | object | Available API endpoint paths |

## Use Cases

1. **Frontend Health Check**: Check if the backend API is available before making requests
2. **DevOps Monitoring**: Use in monitoring tools to verify API availability
3. **Development Testing**: Quick way to verify the API is running during development
4. **Load Balancer Health Check**: Can be used by load balancers to check instance health

## Example Integration

### React Health Check Component
```javascript
import { useEffect, useState } from 'react';

function ApiHealthCheck() {
  const [isHealthy, setIsHealthy] = useState(null);

  useEffect(() => {
    const checkHealth = async () => {
      try {
        const response = await fetch('http://localhost:5000/');
        const data = await response.json();
        setIsHealthy(data.status === 'ok');
      } catch (error) {
        setIsHealthy(false);
      }
    };

    checkHealth();
    const interval = setInterval(checkHealth, 30000); // Check every 30 seconds
    return () => clearInterval(interval);
  }, []);

  if (isHealthy === null) return <div>Checking API status...</div>;
  if (!isHealthy) return <div style={{color: 'red'}}>⚠️ API is unavailable</div>;
  return <div style={{color: 'green'}}>✓ API is running</div>;
}
```

## Notes

- This endpoint does **not** check database connectivity or external service availability
- It only confirms that the API process is running and able to respond to requests
- For production environments, consider implementing more comprehensive health checks that include:
  - Database connectivity
  - External service availability (Google Maps, etc.)
  - Memory and CPU usage
  - Disk space availability
