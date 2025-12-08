# Elevator Number API — Nginx + PHP-FPM Setup

This setup exposes a tiny endpoint that lets a Raspberry Pi post the latest detected elevator number to a server.

- `number.php` accepts authenticated `POST {"value": <number>}` and updates…
- `number.json`, which stores the most recent value + timestamp.
- Optional `index.html` can poll `number.json` for a simple live display.

This README assumes **Ubuntu** on a VPS (e.g., DigitalOcean).

---

## 1) Install Nginx + PHP-FPM

```bash
sudo apt-get update
sudo apt-get install -y nginx php-fpm
sudo systemctl enable --now nginx
```

Check your PHP version:

```bash
php -v
```

---

## 2) Create the project directory

```bash
sudo mkdir -p /var/www/elevator
cd /var/www/elevator
```

---

## 3) Create `number.json`

```bash
sudo tee number.json > /dev/null <<'EOF'
{"value": null, "updated_at": null}
EOF
```

---

## 4) Create `number.php`

**Important:** do not hardcode your real API key in public repos.

```bash
sudo tee number.php > /dev/null <<'EOF'
<?php
header("Content-Type: application/json");

// ================= CONFIG =================
$EXPECTED_API_KEY = "REPLACE_WITH_YOUR_API_KEY";
$DATA_FILE = __DIR__ . "/number.json";
// =========================================

// CORS (optional)
header("Access-Control-Allow-Origin: *");
header("Access-Control-Allow-Headers: Content-Type, X-API-Key");
header("Access-Control-Allow-Methods: POST, OPTIONS");

if ($_SERVER['REQUEST_METHOD'] === 'OPTIONS') {
    http_response_code(204);
    exit;
}

if ($_SERVER['REQUEST_METHOD'] !== 'POST') {
    http_response_code(405);
    echo json_encode(["error" => "Method not allowed"]);
    exit;
}

// API key check
$apiKey = $_SERVER['HTTP_X_API_KEY'] ?? "";
if (!$apiKey || $apiKey !== $EXPECTED_API_KEY) {
    http_response_code(401);
    echo json_encode(["error" => "Unauthorized"]);
    exit;
}

// Parse JSON body
$raw = file_get_contents("php://input");
$body = json_decode($raw, true);

if (!is_array($body) || !isset($body["value"])) {
    http_response_code(400);
    echo json_encode(["error" => "Invalid payload. Expecting {\"value\": number}"]);
    exit;
}

$value = $body["value"];

if (!is_int($value) && !ctype_digit(strval($value))) {
    http_response_code(400);
    echo json_encode(["error" => "Value must be an integer"]);
    exit;
}

$value = intval($value);

// Adjust range to your project needs.
// If your API should only accept single digits, change max to 9.
if ($value < 0 || $value > 99) {
    http_response_code(400);
    echo json_encode(["error" => "Value out of range"]);
    exit;
}

// Write latest state
$data = [
    "value" => $value,
    "updated_at" => gmdate("c")
];

file_put_contents($DATA_FILE, json_encode($data));

echo json_encode(["ok" => true, "stored" => $data]);
EOF
```

---

## 5) Permissions

Allow Nginx/PHP to write the JSON file:

```bash
sudo chown -R www-data:www-data /var/www/elevator
sudo chmod 664 /var/www/elevator/number.json
```

---

## 6) Nginx site config

Create a new server block:

```bash
sudo tee /etc/nginx/sites-available/elevator > /dev/null <<'EOF'
server {
    listen 80;
    server_name _;

    root /var/www/elevator;
    index index.html index.php;

    # Serve static files
    location / {
        try_files $uri $uri/ =404;
    }

    # PHP handling
    location ~ \.php$ {
        include snippets/fastcgi-php.conf;

        # IMPORTANT:
        # Pick the correct PHP-FPM socket for your installed version.
        # Common paths:
        #   /run/php/php8.1-fpm.sock
        #   /run/php/php8.2-fpm.sock
        #   /run/php/php8.3-fpm.sock
        fastcgi_pass unix:/run/php/php8.2-fpm.sock;
    }

    # Prevent access to hidden files
    location ~ /\. {
        deny all;
    }
}
EOF
```

Enable the site:

```bash
sudo ln -s /etc/nginx/sites-available/elevator /etc/nginx/sites-enabled/
sudo nginx -t
sudo systemctl reload nginx
```

### If your PHP socket path differs

List available sockets:

```bash
ls /run/php/
```

Then edit:

```bash
sudo nano /etc/nginx/sites-available/elevator
```

Update `fastcgi_pass` to the correct `phpX.Y-fpm.sock`, then:

```bash
sudo nginx -t
sudo systemctl reload nginx
```

---

## 7) Test with curl

```bash
curl -X POST "http://YOUR_SERVER_IP/number.php" \
  -H "Content-Type: application/json" \
  -H "X-API-Key: REPLACE_WITH_YOUR_API_KEY" \
  -d '{"value": 7}'
```

Check storage:

```bash
curl "http://YOUR_SERVER_IP/number.json"
```

---

## 8) Optional simple display page

```bash
sudo tee /var/www/elevator/index.html > /dev/null <<'EOF'
<!doctype html>
<html>
<head>
  <meta charset="utf-8" />
  <title>Elevator Number</title>
  <style>
    body { font-family: system-ui, sans-serif; padding: 40px; }
    .box { font-size: 64px; font-weight: 700; }
    .meta { color: #666; margin-top: 8px; }
  </style>
</head>
<body>
  <div class="box" id="val">--</div>
  <div class="meta" id="time"></div>

  <script>
    async function refresh() {
      const res = await fetch("./number.json", { cache: "no-store" });
      const data = await res.json();
      document.getElementById("val").textContent =
        (data.value === null || data.value === undefined) ? "--" : data.value;
      document.getElementById("time").textContent =
        data.updated_at ? ("Updated: " + data.updated_at) : "";
    }
    refresh();
    setInterval(refresh, 1000);
  </script>
</body>
</html>
EOF
```

Open:

- `http://YOUR_SERVER_IP/`

---

## 9) Pi-side config reminder

In your YOLO/OCR scripts:

```python
API_URL = "http://YOUR_SERVER_IP/number.php"
API_KEY = "YOUR_API_KEY"
```

---

## Notes

- If your backend should only accept **single digits (0–9)**:
  - Change the PHP range check to `> 9`.
  - Align your Pi-side validation as well.
- For production hardening:
  - Add rate limiting in Nginx.
  - Move the API behind HTTPS (Let’s Encrypt).
  - Store the API key outside the repo (env/config management).

