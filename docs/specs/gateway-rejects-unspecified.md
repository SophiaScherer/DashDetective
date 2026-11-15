# The default gateway is never 0.0.0.0

Work item 131.

## What was wrong

`NetworkGateway.Primary` returned the first IPv4 gateway on the primary adapter without checking for
0.0.0.0, which some adapters report when they have no gateway. The Toolkit's ping and tracert rows and the
Network tab's ping box were then prefilled with "0.0.0.0". The documented contract is `null` for no gateway.
`NetworkUsageSampler.HasUsableGateway` already refused it.

## What was built

- A pure `NetworkGateway.Pick` takes the adapter's gateway addresses and returns the first IPv4 one that is
  not `IPAddress.Any`. `Primary` calls it.

## Decisions

- **IPv6 stays skipped,** including `IPv6Any`: a link-local gateway carries a scope id the Toolkit's host
  validator refuses.
- **Callers are untouched.** They already treat `null` as "no gateway".

## How to verify

1. On an adapter reporting 0.0.0.0 as its gateway, the Network tab's ping box starts empty and the Toolkit
   ping row falls back to its literal host.
2. On a normal connection, the gateway address is prefilled as before.
