# Netlify DNS Manager

A .NET worker service for managing Netlify DNS records. This service automatically updates DNS A records for multiple domains to point to the current public IP address.

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

This application requires the following environment variables:

### NETLIFY_ACCESS_TOKEN
- **Required**: Yes
- **Minimum Length**: 20 characters
- **Description**: Your Netlify access token for API authentication
- **Example**: `NETLIFY_ACCESS_TOKEN=your_netlify_access_token_here`

> [!NOTE]
> The Netlify access token can be optained from this page: [https://app.netlify.com/user/applications](https://app.netlify.com/user/applications#personal-access-tokens).
> The token should be of type "Personal Access Token".

### DOMAIN_01, DOMAIN_02, etc.
- **Required**: Yes (at least one domain)
- **Description**: Domain names to manage DNS records for
- **Example**: 
  ```
  DOMAIN_01=example.com
  DOMAIN_02=yourdomain.com
  DOMAIN_03=anotherdomain.net
  DOMAIN_04=subdomain.anotherdomain.net
  ```

> [!TIP]
> You can have as many domains and subdomains you want in this list. As long as the variable name starts with "DOMAIN" it will be included in the list of domains to set. Remember that all domains and subdomains included here will be configured to point to your own public ip.

### CHECK_INTERVAL (Optional)
- **Default**: 1800 seconds (30 minutes)
- **Description**: Interval in seconds between DNS checks and updates
- **Example**: `CHECK_INTERVAL=3600` (1 hour)

A smaller value can be configured to check for changes more often. For example, a value of `300` would mean every five minutes and would not be a problem for the cpu usage, memory nor the external apis used to check your public ip address.

### ENABLE_LOGGING (Optional)
- **Default**: true
- **Description**: Whether to enable console logging for information level output. Errors will always be logged.
- **Example**: `ENABLE_LOGGING=true`

> [!TIP]
> A value of true for ENABLE_LOGGING is nice when setting up the service for the first time to see detailed output of what's happening to confirm everything is working as it should. This can then later be changed to false to not flood the logs over time.

## Configuration

The application uses EasyReasy.EnvironmentVariables for environment variable validation. All required environment variables are validated at startup, and the application will fail to start if any required variables are missing or invalid. The application will clearly report what variables are missing.

### Domain Configuration

The application supports multiple domains using the `DOMAIN_` prefix pattern:
- `DOMAIN_01`, `DOMAIN_02`, `DOMAIN_03`, etc.
- At least one domain must be configured
- The application will automatically detect all domains with this prefix

### Example Environment Configuration

```bash
# Required
NETLIFY_ACCESS_TOKEN=your_netlify_access_token_here
DOMAIN_01=example.com
DOMAIN_02=yourdomain.com

# Optional
CHECK_INTERVAL=1800
ENABLE_LOGGING=true
```

# Development

To run the application in development:

1. Set the `NETLIFY_ACCESS_TOKEN` environment variable with your Netlify access token
2. Run the application using `dotnet run`

## Testing

The project includes comprehensive integration tests that use real Netlify API calls. To run the tests, you need to create an environment variables file.

### Test Environment Variables

Create a file named `environment-variables.txt` in the project root directory with the following content:

```
NETLIFY_ACCESS_TOKEN=your_actual_netlify_access_token_here
TEST_DOMAIN=your_actual_test_domain_here
```

### Required Test Variables

#### NETLIFY_ACCESS_TOKEN
- **Required**: Yes
- **Minimum Length**: 20 characters
- **Description**: Your Netlify access token for API authentication
- **Note**: This should be the same token used for the main application

> [!NOTE]
> The Netlify access token can be optained from this page: [https://app.netlify.com/user/applications](https://app.netlify.com/user/applications#personal-access-tokens).
> The token should be of type "Personal Access Token".

#### TEST_DOMAIN
- **Required**: Yes
- **Minimum Length**: 5 characters
- **Description**: The domain name to use for DNS record testing
- **Example**: `example.com` or `yourdomain.com`
- **Note**: This domain should be configured in your Netlify account for DNS management

### Running Tests

1. Create the `environment-variables.txt` file with your actual values
2. Run the tests using: `dotnet test`
3. The tests will output the absolute path where it's looking for the environment file

### Test Behavior

The integration tests will:
- Create temporary DNS records for testing
- Verify the records were created correctly
- Delete the test records after verification
- Test error conditions and validation

**Warning**: These tests make real API calls to Netlify and will create/delete actual DNS records on your test domain. Use a domain you control and are prepared to have test records added to.
