"use strict";

// Loopback-only Cloudinary uploader for legacy Windows hosts where .NET's
// Schannel cannot establish a TLS session with Cloudinary. The bridge keeps
// credentials on the server and uses Node's TLS implementation.
const crypto = require("crypto");
const fs = require("fs");
const http = require("http");
const https = require("https");


const host = "127.0.0.1";
const port = Number(process.env.CLOUDINARY_BRIDGE_PORT || 5290);
const configPath = process.env.SNM_BACKEND_CONFIG || "C:\\SNM\\backend\\appsettings.Production.json";

function config() {
  // Windows PowerShell 5.1 writes UTF-8 files with a BOM by default. Strip
  // that marker defensively so deployment-generated appsettings files cannot
  // break all image upload endpoints.
  const rawConfig = fs.readFileSync(configPath, "utf8").replace(/^\uFEFF/, "");
  const settings = JSON.parse(rawConfig);
  const cloudinary = settings.Cloudinary || {};
  if (!cloudinary.CloudName || !cloudinary.ApiKey || !cloudinary.ApiSecret) {
    throw new Error("Cloudinary configuration is incomplete.");
  }
  return cloudinary;
}

function send(response, statusCode, body) {
  response.writeHead(statusCode, { "Content-Type": "application/json; charset=utf-8" });
  response.end(JSON.stringify(body));
}

function readBody(request) {
  return new Promise((resolve, reject) => {
    let raw = "";
    request.setEncoding("utf8");
    request.on("data", (chunk) => {
      raw += chunk;
      if (raw.length > 16 * 1024 * 1024) {
        reject(new Error("Upload request is too large."));
        request.destroy();
      }
    });
    request.on("end", () => resolve(JSON.parse(raw || "{}")));
    request.on("error", reject);
  });
}

function multipartRequest(cloudinary, values, bytes, contentType, resourceType) {
  return new Promise((resolve, reject) => {
    const boundary = `----snm-${crypto.randomBytes(12).toString("hex")}`;
    const parts = [];
    for (const [key, value] of Object.entries(values)) {
      parts.push(Buffer.from(`--${boundary}\r\nContent-Disposition: form-data; name="${key}"\r\n\r\n${value}\r\n`));
    }
    parts.push(Buffer.from(`--${boundary}\r\nContent-Disposition: form-data; name="file"; filename="upload"\r\nContent-Type: ${contentType}\r\n\r\n`));
    parts.push(bytes);
    parts.push(Buffer.from(`\r\n--${boundary}--\r\n`));
    const body = Buffer.concat(parts);
    const request = https.request({
      hostname: "api.cloudinary.com",
      path: `/v1_1/${encodeURIComponent(cloudinary.CloudName)}/${resourceType}/upload`,
      method: "POST",
      headers: { "Content-Type": `multipart/form-data; boundary=${boundary}`, "Content-Length": body.length },
      timeout: 30000,
    }, (upstream) => {
      let raw = "";
      upstream.setEncoding("utf8");
      upstream.on("data", (chunk) => { raw += chunk; });
      upstream.on("end", () => {
        try {
          const result = JSON.parse(raw || "{}");
          if (upstream.statusCode >= 200 && upstream.statusCode < 300 && result.secure_url) resolve(result.secure_url);
          else reject(new Error(result.error?.message || `Cloudinary returned HTTP ${upstream.statusCode}.`));
        } catch (error) { reject(error); }
      });
    });
    request.on("timeout", () => request.destroy(new Error("Cloudinary upload timed out.")));
    request.on("error", reject);
    request.end(body);
  });
}

const server = http.createServer(async (request, response) => {
  const remote = request.socket.remoteAddress;
  if (remote !== "127.0.0.1" && remote !== "::1" && remote !== "::ffff:127.0.0.1") {
    return send(response, 403, { message: "Loopback access only." });
  }
  if (request.method === "GET" && request.url === "/health") return send(response, 200, { status: "ok" });
  if (request.method !== "POST" || request.url !== "/upload") return send(response, 404, { message: "Not found." });
  try {
    const payload = await readBody(request);
    const category = String(payload.category || "uploads").replace(/[^a-zA-Z0-9_\-/]/g, "-");
    const contentType = String(payload.contentType || "application/octet-stream");
    const resourceType = payload.resourceType === "raw" ? "raw" : "image";
    const bytes = Buffer.from(String(payload.fileBase64 || ""), "base64");
    if (!bytes.length) throw new Error("No upload data was supplied.");
    const cloudinary = config();
    const timestamp = Math.floor(Date.now() / 1000).toString();
    const publicId = `${cloudinary.RootFolder || "smart-night-market"}/${category}/${crypto.randomUUID()}`;
    const signatureBase = `public_id=${publicId}&timestamp=${timestamp}${cloudinary.ApiSecret}`;
    const signature = crypto.createHash("sha1").update(signatureBase).digest("hex");
    const secureUrl = await multipartRequest(cloudinary, { api_key: cloudinary.ApiKey, public_id: publicId, timestamp, signature }, bytes, contentType, resourceType);
    return send(response, 200, { secureUrl });
  } catch (error) {
    console.error(`[cloudinary-bridge] ${error.message}`);
    return send(response, 502, { message: "Cloudinary upload failed." });
  }
});

server.listen(port, host, () => console.log(`[cloudinary-bridge] listening on http://${host}:${port}`));
