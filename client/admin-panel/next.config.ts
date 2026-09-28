import type { NextConfig } from "next"

const apiUrl = process.env.NEXT_PUBLIC_API_URL ?? "http://localhost:5101/api"
const apiOrigin = apiUrl.replace(/\/api\/?$/, "")
const publicImageUrl = process.env.NEXT_PUBLIC_IMAGE_URL?.trim().replace(/\/$/, "")

let imageHostname = "localhost"
let imageProtocol: "http" | "https" = "http"
let imagePort: string | undefined = "5101"
let publicImagePattern:
  | {
      protocol: "http" | "https"
      hostname: string
      port?: string
      pathname: string
    }
  | undefined

try {
  const parsed = new URL(apiOrigin)
  imageHostname = parsed.hostname
  imageProtocol = parsed.protocol === "https:" ? "https" : "http"
  imagePort = parsed.port || undefined
} catch {
  // keep localhost defaults
}

if (publicImageUrl) {
  try {
    const parsed = new URL(publicImageUrl)
    if (parsed.protocol !== "http:" && parsed.protocol !== "https:") {
      throw new Error("unsupported protocol")
    }
    publicImagePattern = {
      protocol: parsed.protocol.slice(0, -1) as "http" | "https",
      hostname: parsed.hostname,
      ...(parsed.port ? { port: parsed.port } : {}),
      pathname: "/**",
    }
  } catch {
    throw new Error("NEXT_PUBLIC_IMAGE_URL must be an absolute http or https URL")
  }
}

/**
 * Content-Security-Policy and the app-level response headers.
 *
 * Transport headers (HSTS, X-Frame-Options, nosniff, Referrer-Policy) are set
 * once in deploy/traefik/dynamic/routes.yml. CSP lives here instead because it
 * has to name this app's own API origin, and because Next.js needs
 * 'unsafe-inline' for the styles and hydration payload it emits.
 *
 * 'unsafe-inline' in script-src is the known weak point: removing it requires
 * per-request nonces threaded through the app, which is a separate change.
 * The policy still blocks the loading of third-party script origins, which is
 * what an injected <script src> would need.
 */
function securityHeaders() {
  const csp = [
    "default-src 'self'",
    "base-uri 'self'",
    "object-src 'none'",
    "frame-ancestors 'none'",
    "form-action 'self'",
    "img-src 'self' data: blob: " + apiOrigin + (publicImageUrl ? " " + publicImageUrl : ""),
    "font-src 'self' data:",
    "style-src 'self' 'unsafe-inline'",
    "script-src 'self' 'unsafe-inline'",
    "connect-src 'self' " + apiOrigin,
  ].join("; ")

  return [
    { key: "Content-Security-Policy", value: csp },
    // Repeated from the proxy on purpose: the admin panel must still carry them if
    // it is ever run without Traefik in front.
    { key: "X-Content-Type-Options", value: "nosniff" },
    { key: "Referrer-Policy", value: "strict-origin-when-cross-origin" },
    { key: "X-Frame-Options", value: "DENY" },
  ]
}

const nextConfig: NextConfig = {
  images: {
    remotePatterns: [
      {
        protocol: imageProtocol,
        hostname: imageHostname,
        ...(imagePort ? { port: imagePort } : {}),
        pathname: "/img/**",
      },
      {
        protocol: "http",
        hostname: "localhost",
        port: "5101",
        pathname: "/img/**",
      },
      {
        protocol: "http",
        hostname: "127.0.0.1",
        port: "5101",
        pathname: "/img/**",
      },
      ...(publicImagePattern ? [publicImagePattern] : []),
    ],
  },
  async headers() {
    return [{ source: "/:path*", headers: securityHeaders() }]
  },
  output: "standalone",
}

export default nextConfig
