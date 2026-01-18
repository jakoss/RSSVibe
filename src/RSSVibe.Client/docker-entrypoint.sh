#!/bin/sh
set -e

# Validate required environment variable
if [ -z "$API_BASE_URL" ]; then
    echo "================================"
    echo "ERROR: Missing Configuration"
    echo "================================"
    echo ""
    echo "The API_BASE_URL environment variable is required for Docker deployment."
    echo ""
    echo "Example usage:"
    echo "  docker run -e API_BASE_URL=https://api.example.com ghcr.io/you/rssvibe-client:latest"
    echo ""
    echo "For Kubernetes:"
    echo "  env:"
    echo "    - name: API_BASE_URL"
    echo "      value: \"https://api.example.com\""
    echo ""
    exit 1
fi

# Generate appsettings.json with API configuration
cat > /usr/share/nginx/html/appsettings.json <<EOF
{
  "ApiBaseUrl": "${API_BASE_URL}"
}
EOF

echo "✓ Configuration generated successfully"
echo "  API Base URL: ${API_BASE_URL}"

# Start nginx in foreground
exec nginx -g 'daemon off;'
