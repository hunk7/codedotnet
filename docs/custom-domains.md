# Custom Domains

This document provides DNS/CNAME setup guidance for serving codedotnet on custom domains via
GitHub Pages, per §17.3 of the requirements. This is operational/administrative guidance — it is
not automated by CI and must be applied manually in your DNS provider and the repository's
GitHub Pages settings.

## Recommended arrangement

- **`codedotnet.in`** — canonical domain, configured directly as the GitHub Pages custom domain.
- **`www.codedotnet.in`** — redirects to the canonical domain (`codedotnet.in`).
- **`codedotnet.com`** — redirects to `codedotnet.in`. GitHub Pages only supports one custom
  domain being configured directly per Pages site, so `codedotnet.com` must use registrar/DNS-level
  forwarding or a lightweight redirect service rather than being added as a second Pages custom
  domain.
- **`www.codedotnet.com`** — redirects appropriately (to `codedotnet.com`, which in turn
  redirects to `codedotnet.in`, or directly to `codedotnet.in`).

## DNS records for the canonical domain (`codedotnet.in`)

For an apex/root domain pointing at GitHub Pages, create `A` records pointing at GitHub's Pages
IP addresses (check GitHub's current documented list before applying, as these can change):

```
A     @     185.199.108.153
A     @     185.199.109.153
A     @     185.199.110.153
A     @     185.199.111.153
```

For the `www` subdomain, use a `CNAME` record pointing at the GitHub Pages hostname:

```
CNAME  www   <username>.github.io.
```

In the repository's **Settings → Pages → Custom domain**, set the custom domain to
`codedotnet.in` and enable "Enforce HTTPS" once GitHub reports the domain as verified (DNS
propagation can take up to 24–48 hours). This also creates/updates a `CNAME` file in the
published Pages branch/artifact containing `codedotnet.in`.

## Redirecting `www.codedotnet.in` to `codedotnet.in`

Most DNS/domain registrars support one of:

- A `CNAME` + GitHub Pages will serve the same site under `www`, but GitHub Pages does not
  automatically redirect `www` to the apex (or vice versa) — configure a redirect at the
  registrar/DNS level (many providers offer "URL forwarding" or "domain forwarding" features) so
  `www.codedotnet.in` issues an HTTP redirect to `https://codedotnet.in`.

## Redirecting `codedotnet.com` (and `www.codedotnet.com`) to `codedotnet.in`

Since only one custom domain can be configured directly against the GitHub Pages site, use one
of:

- **Registrar-level domain forwarding** — most registrars (e.g. Namecheap, GoDaddy, Cloudflare
  Registrar) offer free HTTP(S) forwarding from a parked domain to an external URL. Point
  `codedotnet.com` (and `www.codedotnet.com`) to `https://codedotnet.in`.
- **A lightweight redirect service** — e.g. Cloudflare Workers, Netlify redirects, or a similar
  edge-redirect service, if more control over the redirect (status code, path preservation) is
  needed than basic registrar forwarding provides.

## HTTPS enforcement

Per §17.4, GitHub Pages' "Enforce HTTPS" setting should only be enabled after the custom domain's
DNS configuration has been verified by GitHub (the checkbox is greyed out until verification
succeeds). Until DNS is verified, continue using the default
`https://<username>.github.io/codedotnet/` URL, which already serves over HTTPS.

## Decision to document

The final choice of canonical domain (`codedotnet.in` vs. `codedotnet.com`) and the specific
redirect mechanism used for the non-canonical domain(s) should be recorded here once decided;
this document currently reflects the recommended default arrangement from §17.3 and should be
updated to match whatever is actually configured in the registrar and GitHub Pages settings.
