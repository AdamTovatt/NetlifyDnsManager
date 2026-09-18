# Netlify DNS Manager

A .NET service for managing Netlify DNS records. This service automatically updates DNS A records for multiple domains to point to the current address of the host, which is its public IP address by default, or the address of a named local network interface when `IP_SOURCE_INTERFACE` is set.

It supports three operating modes:
- **None** (default) — checks own address and updates Netlify directly
- **Server** — does everything the default mode does, plus runs a web API that accepts DNS update requests and ACME challenge records from authenticated clients
- **Client** — checks own address and reports it to a remote server instead of updating Netlify directly

The server/client modes allow you to run a single centralized instance that holds the Netlify API token, while remote instances (clients) report their IP addresses to the server. Each client authenticates with a scoped API key that only allows updating specific domains. This avoids sharing your Netlify access token.

## Installation

There are two ways to install the Netlify DNS Manager service: using the install script or using [Updaemon](https://github.com/AdamTovatt/updaemon).

### Option 1: Updaemon (Recommended)

If you have [Updaemon](https://github.com/AdamTovatt/updaemon) installed, you can use it to manage the service and get automatic updates:

1. Register the service with updaemon:
   ```bash
   sudo updaemon new netlify-dns-manager --from github --remote AdamTovatt/NetlifyDnsManager
   ```

2. Download and set up the service:
   ```bash
   sudo updaemon init netlify-dns-manager
   ```

3. Configure the environment variables in `/etc/systemd/system/netlify-dns-manager.service` (see the [Environment Variables](#environment-variables) section below), then reload and restart:
   ```bash
   sudo systemctl daemon-reload
   sudo systemctl restart netlify-dns-manager
   ```

### Option 2: Install Script

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

#### IP_SOURCE_INTERFACE (Optional)
- **Default**: Not set, meaning the address is discovered from public IP services
- **Description**: The name of a local network interface to read the published address from. Use this for a host whose useful address is private, such as one reachable over a VPN or only on a LAN.
- **Example**: `IP_SOURCE_INTERFACE=tailscale0`

The interface is named directly, so any name the operating system lists works: `wg0`, `eth0`, `tailscale0`, `end0`. Run `ip -brief address` on Linux, or `ifconfig` on macOS, to see the names and addresses on your host. If the interface has several IPv4 addresses, the first one the operating system lists is published.

> [!IMPORTANT]
> When this variable is set the public IP services are not used at all. If the configured interface cannot be read, because it does not exist, has no IPv4 address, or has only a self-assigned `169.254.x.x` address, nothing is published for that cycle and the error is logged. There is deliberately no fallback to the public IP: a fallback would publish the host's public address and then correct itself on a later cycle, leaving a name that resolves publicly part of the time while looking correct whenever it is checked.

The startup log line names the source in use, so `AddressSource=network interface wg0` confirms the variable took effect.

#### CHECK_INTERVAL (Optional)
- **Default**: 1800 seconds (30 minutes)
- **Description**: Interval in seconds between DNS checks and updates
- **Example**: `CHECK_INTERVAL=300` (5 minutes)

A smaller value can be configured to check for changes more often. For example, a value of `300` would mean every five minutes and would not be a problem for the cpu usage, memory nor the external apis used to check your public ip address.

This interval is also how long the service waits after a failed check before trying again, so a smaller value is worth setting on a host whose address source can be unavailable for a while after boot, such as a VPN interface that takes time to come up.

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

This is the original behavior. The service checks its own address and updates Netlify DNS directly.

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

Server mode does everything the default mode does (manages its own domains via Netlify), plus runs a web API that accepts DNS update requests and ACME challenge records from authenticated clients.

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

Each client can only update the domains listed in their `allowedDomains`, and can only publish an ACME challenge record under one of those domains. Attempting to touch any other domain will return a 403 Forbidden response.

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

Client mode does NOT talk to Netlify. Instead, it checks its own address and reports it to a remote server. The client only sends a request when that address actually changes.

> [!NOTE]
> In client mode, no Netlify access token is needed. The server handles all communication with Netlify.

> [!TIP]
> If the useful address of this host is private, for example an address on a VPN, set `IP_SOURCE_INTERFACE` so the client reports that interface's address instead of its public IP.

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
Environment=IP_SOURCE_INTERFACE=tailscale0
Environment=CHECK_INTERVAL=300
Environment=ENABLE_LOGGING=true
```

## How It Works

### IP Address Detection

By default the service uses three external services for redundancy when detecting the public IP:
- `https://icanhazip.com`
- `https://api.ipify.org`
- `https://ipv4.seeip.org`

All three are queried concurrently, and the first valid response is used.

If `IP_SOURCE_INTERFACE` is set, the address is read from that local network interface instead and the external services are not used. The interface reader replaces the public IP services rather than being added to them, so a failed interface read means no DNS update for that cycle — see the [IP_SOURCE_INTERFACE](#ip_source_interface-optional) section for why there is no fallback.

### DNS Update Flow

When the IP address changes (or on first run):

1. Fetch all DNS records for the domain from Netlify
2. Find the existing A record for the domain
3. If the A record already points to the current IP, skip (no-op)
4. If it differs, delete the old A record and create a new one with the current IP (TTL 1800s)
5. If no A record exists, create a new one

### Client/Server Flow

1. **Client** detects its address (the public IP by default, or the address of the interface named by `IP_SOURCE_INTERFACE`)
2. **Client** caches the last reported address and only contacts the server when it changes
3. **Client** authenticates with the server using its API key and receives a JWT
4. **Client** sends `POST /api/dns/update` with `{ "domain": "...", "ip": "..." }` and receives `{ "domain": "...", "ip": "...", "updated": true }`
5. **Server** validates the JWT, checks the requested domain is in the client's allowed list
6. **Server** performs the DNS update on Netlify on behalf of the client

### Certificates for hosts that are not publicly reachable (ACME DNS-01)

A host whose address is private cannot answer Let's Encrypt's HTTP-01 challenge, because nothing on the public internet can reach it. The DNS-01 challenge works instead: the ACME client is given a value to publish as a TXT record at `_acme-challenge.<domain>`, and the certificate is issued once the ACME server can look it up.

The server publishes and removes that record on a client's behalf, so a client still needs no Netlify token of its own. Both endpoints authenticate with the same API key and authorize the same way the update endpoint does.

**Publish a challenge value**

```bash
TOKEN=$(curl -s -X POST https://yourdomain.com/dns-manager/api/auth/apikey \
  -H "Content-Type: application/json" \
  -d '{"apiKey":"a-long-random-api-key-given-to-you"}' | jq -r .token)

curl -s -X POST https://yourdomain.com/dns-manager/api/dns/challenge \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{"domain":"yoursubdomain.yourdomain.com","value":"the-value-from-your-acme-client"}'
```

Both calls answer with the record the server acted on:

```json
{ "domain": "yoursubdomain.yourdomain.com", "recordName": "_acme-challenge.yoursubdomain.yourdomain.com", "created": true }
```

`recordName` is the name the value was actually published at, and `created` is false when that value was already there. `POST /api/dns/update` answers in the same shape, with `updated` in place of `created`.

**Remove it again after validation**

```bash
curl -s -X DELETE "https://yourdomain.com/dns-manager/api/dns/challenge?domain=yoursubdomain.yourdomain.com" \
  -H "Authorization: Bearer $TOKEN"
```

The response reports `deleted`, the number of records removed. Add `&value=$CERTBOT_VALIDATION` to remove one value instead of every value at that name.

With certbot, the first call belongs in `--manual-auth-hook` using `$CERTBOT_DOMAIN` and `$CERTBOT_VALIDATION`, and the second in `--manual-cleanup-hook`, which needs only `$CERTBOT_DOMAIN` unless you scope the removal to one value.

> [!NOTE]
> This is a server-mode API that a client calls when its ACME client asks it to. Nothing in `PROXY_MODE=client` requests certificates on its own: the client only reports its address, and the certificate is obtained by whatever ACME client runs on the host.

A few properties worth knowing:

- **The client never names the record.** It sends the domain it is authorized for, and the server writes `_acme-challenge.<domain>`. A key scoped to `friend1.yourdomain.com` can therefore affect `_acme-challenge.friend1.yourdomain.com` and no other challenge name in the zone, and any other name is refused with 403. The domain may be given in any case; the spelling from the clients configuration is what the record is named after.
- **Publishing is additive.** A second value at the same record name is added rather than replacing the first, so a certificate covering both a name and its wildcard can be validated in one go. Publishing a value that is already there changes nothing and reports `created: false`. Values are published with a 60 second TTL, so a removed challenge stops being served quickly.
- **A name holds at most four values.** Beyond that the request is refused with 409 Conflict rather than growing the record set further, because reaching four means earlier challenges were never cleaned up. Remove them and publish again.
- **Removal takes away only challenge records.** `DELETE` removes the TXT records at `_acme-challenge.<domain>`, leaving the domain's own records, and any `_acme-challenge` delegation record, untouched. A failure to remove them is reported as 500 rather than as a successful cleanup; a `deleted` count of 0 means there was nothing published to remove, which is also what a value-scoped removal reports when that value is not there.
- **The periodic A record update ignores them.** The record it manages is the `A` record at the domain itself, so a challenge published during a renewal is not disturbed.
- **Value limits.** A challenge value must be at most 255 characters, the longest a single DNS TXT string can be; a longer one is refused with 400.

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

Unit tests cover the proxy mode functionality and the address sources, and can be run without any external services or credentials. Every test class that is not marked `[TestCategory(TestCategories.Integration)]` is a unit test, so a new test class is included here and in CI without any list to update.

Run unit tests:
```bash
dotnet test --filter "TestCategory!=Integration"
```

Tests that call live external services are marked with `[TestCategory(TestCategories.Integration)]` and are excluded by that filter.

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
