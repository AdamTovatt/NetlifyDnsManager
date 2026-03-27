# Netlify DNS Manager

A .NET service for managing Netlify DNS records. This service automatically updates DNS A records for multiple domains to point to the current public IP address.

It supports three operating modes:
- **None** (default) — checks own public IP and updates Netlify directly
- **Server** — does everything the default mode does, plus runs a web API that accepts DNS update requests from authenticated clients
- **Client** — checks own public IP and reports it to a remote server instead of updating Netlify directly

The server/client modes allow you to run a single centralized instance that holds the Netlify API token, while remote instances (clients) report their IP addresses to the server. Each client authenticates with a scoped API key that only allows updating specific domains. This avoids sharing your Netlify access token.

## Installation

To install the Netlify DNS Manager service:

1. Run the installation command:
   ```bash
   wget -qO /tmp/install.sh https://raw.githubusercontent.com/AdamTovatt/NetlifyDnsManager/refs/heads/master/install.sh && sudo bash /tmp/install.sh
   ```

2. Configure the service in `/etc/systemd/system/netlify-dns-manager.service` for example by running:
   ```
   sudo nano /etc/systemd/system/netlify-dns-manager.service
   ```
> [!NOTE]
> You may want to change the configured user that runs the service. It's probably set to "root" by default, but for example on a raspberry pi you might want the user "pi". But it's up to you.

> [!IMPORTANT]
> Don't foget to update the environment variables as needed before continuing to the next step.
> More information about the environment variables can be found further down.

3. When you have configured the environment variables, reload the systemd daemon:
   ```bash
   sudo systemctl daemon-reload
   ```

4. Enable and start the service:
   ```bash
   sudo systemctl enable --now netlify-dns-manager.service
   ```

5. (Optional) Check the service logs:
   ```bash
   sudo journalctl -u netlify-dns-manager.service -n 30
   ```

## Environment Variables

### Common Variables (all modes)

#### DOMAIN_01, DOMAIN_02, etc.
- **Required**: Yes (at least one domain)
- **Description**: Domain names to manage DNS records for
- **Example**:
  ```
  DOMAIN_01=example.com
  DOMAIN_02=yourdomain.com
  DOMAIN_03=subdomain.yourdomain.com
  ```

> [!TIP]
> You can have as many domains and subdomains you want in this list. As long as the variable name starts with "DOMAIN" it will be included in the list of domains to set.

#### CHECK_INTERVAL (Optional)
- **Default**: 1800 seconds (30 minutes)
- **Description**: Interval in seconds between DNS checks and updates
- **Example**: `CHECK_INTERVAL=300` (5 minutes)

A smaller value can be configured to check for changes more often. For example, a value of `300` would mean every five minutes and would not be a problem for the cpu usage, memory nor the external apis used to check your public ip address.

#### ENABLE_LOGGING (Optional)
- **Default**: true
- **Description**: Whether to enable console logging for information level output. Errors will always be logged.
- **Example**: `ENABLE_LOGGING=true`

> [!TIP]
> A value of true for ENABLE_LOGGING is nice when setting up the service for the first time to see detailed output of what's happening to confirm everything is working as it should. This can then later be changed to false to not flood the logs over time.

#### PROXY_MODE (Optional)
- **Default**: `none`
- **Description**: The operating mode. Valid values: `none`, `server`, `client`
- **Example**: `PROXY_MODE=server`

### Default Mode Variables (`PROXY_MODE=none` or not set)

This is the original behavior. The service checks its own public IP and updates Netlify DNS directly.

#### NETLIFY_ACCESS_TOKEN
- **Required**: Yes
- **Minimum Length**: 20 characters
- **Description**: Your Netlify access token for API authentication
- **Example**: `NETLIFY_ACCESS_TOKEN=your_netlify_access_token_here`

> [!NOTE]
> The Netlify access token can be optained from this page: [https://app.netlify.com/user/applications](https://app.netlify.com/user/applications#personal-access-tokens).
> The token should be of type "Personal Access Token".

#### Example (default mode)

```
Environment=NETLIFY_ACCESS_TOKEN=your_netlify_access_token_here
Environment=DOMAIN_01=example.com
Environment=DOMAIN_02=yourdomain.com
Environment=CHECK_INTERVAL=300
Environment=ENABLE_LOGGING=true
```

### Server Mode Variables (`PROXY_MODE=server`)

Server mode does everything the default mode does (manages its own domains via Netlify), plus runs a web API that accepts DNS update requests from authenticated clients.

#### NETLIFY_ACCESS_TOKEN
- **Required**: Yes
- **Description**: Same as default mode. The server uses this token to update DNS records on behalf of itself and its clients.

#### JWT_SECRET
- **Required**: Yes
- **Minimum Length**: 32 characters
- **Description**: The secret key used to sign JWT tokens for client authentication. Must be at least 256 bits (32 bytes) for HS256.

#### CLIENTS_CONFIG_PATH
- **Required**: Yes
- **Description**: Path to a JSON file that defines authorized clients and their allowed domains.
- **Example**: `CLIENTS_CONFIG_PATH=/etc/netlify-dns-manager/clients.json`

#### API_PORT (Optional)
- **Default**: 5050
- **Description**: The port the web API listens on.
- **Example**: `API_PORT=5040`

#### Clients Configuration File

The file specified by `CLIENTS_CONFIG_PATH` defines which API keys are valid and which domains each key is allowed to update:

```json
{
  "clients": [
    {
      "apiKey": "a-long-random-api-key-for-friend-1",
      "allowedDomains": ["friend1.yourdomain.com", "friend1-alt.yourdomain.com"],
      "name": "Friend 1"
    },
    {
      "apiKey": "a-long-random-api-key-for-friend-2",
      "allowedDomains": ["friend2.yourdomain.com"],
      "name": "Friend 2"
    }
  ]
}
```

Each client can only update the domains listed in their `allowedDomains`. Attempting to update any other domain will return a 403 Forbidden response.

#### Example (server mode)

```
Environment=PROXY_MODE=server
Environment=NETLIFY_ACCESS_TOKEN=your_netlify_access_token_here
Environment=JWT_SECRET=a-secret-that-is-at-least-32-characters-long
Environment=CLIENTS_CONFIG_PATH=/etc/netlify-dns-manager/clients.json
Environment=API_PORT=5040
Environment=DOMAIN_01=example.com
Environment=DOMAIN_02=yourdomain.com
Environment=CHECK_INTERVAL=300
Environment=ENABLE_LOGGING=true
```

### Client Mode Variables (`PROXY_MODE=client`)

Client mode does NOT talk to Netlify. Instead, it checks its own public IP and reports it to a remote server. The client only sends a request when its IP address actually changes.

> [!NOTE]
> In client mode, no Netlify access token is needed. The server handles all communication with Netlify.

#### PROXY_SERVER_URL
- **Required**: Yes
- **Description**: The URL of the remote server to send DNS update requests to.
- **Example**: `PROXY_SERVER_URL=https://yourdomain.com/dns-manager`

#### PROXY_API_KEY
- **Required**: Yes
- **Minimum Length**: 10 characters
- **Description**: The API key for authenticating with the remote server. This key must match one of the entries in the server's clients configuration file.

#### Example (client mode)

```
Environment=PROXY_MODE=client
Environment=PROXY_SERVER_URL=https://yourdomain.com/dns-manager
Environment=PROXY_API_KEY=a-long-random-api-key-given-to-you
Environment=DOMAIN_01=yoursubdomain.yourdomain.com
Environment=CHECK_INTERVAL=300
Environment=ENABLE_LOGGING=true
```

## How It Works

### IP Address Detection

The service uses three external services for redundancy when detecting the public IP:
- `https://icanhazip.com`
- `https://api.ipify.org`
- `https://ipv4.seeip.org`

All three are queried concurrently, and the first valid response is used.

### DNS Update Flow

When the IP address changes (or on first run):

1. Fetch all DNS records for the domain from Netlify
2. Find the existing A record for the domain
3. If the A record already points to the current IP, skip (no-op)
4. If it differs, delete the old A record and create a new one with the current IP (TTL 1800s)
5. If no A record exists, create a new one

### Client/Server Flow

1. **Client** detects its public IP (same three-service approach)
2. **Client** caches the last reported IP and only contacts the server when it changes
3. **Client** authenticates with the server using its API key and receives a JWT
4. **Client** sends `POST /api/dns/update` with `{ "domain": "...", "ip": "..." }`
5. **Server** validates the JWT, checks the requested domain is in the client's allowed list
6. **Server** performs the DNS update on Netlify on behalf of the client

## Configuration

The application uses EasyReasy.EnvironmentVariables for environment variable validation. All required environment variables are validated at startup, and the application will fail to start if any required variables are missing or invalid. The application will clearly report what variables are missing.

### Domain Configuration

The application supports multiple domains using the `DOMAIN_` prefix pattern:
- `DOMAIN_01`, `DOMAIN_02`, `DOMAIN_03`, etc.
- At least one domain must be configured
- The application will automatically detect all domains with this prefix

# Development

To run the application in development:

1. Set the required environment variables for the mode you want to test
2. Run the application using `dotnet run`

## Testing

The project includes both unit tests and integration tests.

### Unit Tests

Unit tests cover the proxy mode functionality and can be run without any external services or credentials:

- **ClientsConfiguration** — loading and querying the clients JSON config
- **ClientAuthValidationService** — API key validation and JWT claim generation
- **DnsUpdateService** — DNS record update logic (create, update, skip)
- **DnsUpdateEndpointAuthorization** — domain authorization via JWT claims
- **ClientWorkerIpCaching** — IP caching behavior (only report when changed)

Run unit tests:
```bash
dotnet test --filter "FullyQualifiedName~ClientsConfiguration|FullyQualifiedName~ClientAuthValidation|FullyQualifiedName~DnsUpdateService|FullyQualifiedName~DnsUpdateEndpointAuthorization|FullyQualifiedName~ClientWorkerIpCaching"
```

### Integration Tests

Integration tests use real Netlify API calls. To run them, you need to create an environment variables file.

#### Test Environment Variables

Create a file named `environment-variables.txt` in the project root directory with the following content:

```
NETLIFY_ACCESS_TOKEN=your_actual_netlify_access_token_here
TEST_DOMAIN=your_actual_test_domain_here
```

#### Required Test Variables

##### NETLIFY_ACCESS_TOKEN
- **Required**: Yes
- **Minimum Length**: 20 characters
- **Description**: Your Netlify access token for API authentication
- **Note**: This should be the same token used for the main application

> [!NOTE]
> The Netlify access token can be optained from this page: [https://app.netlify.com/user/applications](https://app.netlify.com/user/applications#personal-access-tokens).
> The token should be of type "Personal Access Token".

##### TEST_DOMAIN
- **Required**: Yes
- **Minimum Length**: 5 characters
- **Description**: The domain name to use for DNS record testing
- **Example**: `example.com` or `yourdomain.com`
- **Note**: This domain should be configured in your Netlify account for DNS management

#### Running Tests

1. Create the `environment-variables.txt` file with your actual values
2. Run the tests using: `dotnet test`
3. The tests will output the absolute path where it's looking for the environment file

#### Test Behavior

The integration tests will:
- Create temporary DNS records for testing
- Verify the records were created correctly
- Delete the test records after verification
- Test error conditions and validation

**Warning**: These tests make real API calls to Netlify and will create/delete actual DNS records on your test domain. Use a domain you control and are prepared to have test records added to.
