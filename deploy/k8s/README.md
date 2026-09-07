# Kubernetes deployment

Full multi-replica horizontal scaling for a self-hosted install - the cloud-agnostic equivalent of an
Azure Container Apps + KEDA setup, for anyone who wants real scaling without cloud lock-in.

**Most installs don't need this.** `docker-compose.yml`/`docker-compose.large.yml` (see the repo
root README's "Self-hosted install" section, or just run `install.sh`) is the right starting point -
one node, one container per app, zero orchestration to operate. Reach for this directory only once a
single Agrumy.Api/Agrumy.Web pair is genuinely not enough capacity for your fleet.

## What this does and doesn't cover

Covers: Deployment + Service for `Agrumy.Api` and `Agrumy.Web` (replica count configurable, rolling
updates), a ConfigMap/Secret pair replacing `appsettings.json`, an optional `HorizontalPodAutoscaler`
per app, an `Ingress` with TLS termination (replaces the nginx/Apache reverse-proxy templates for this
deployment mode), and a single-instance Redis (roadmap #72's distributed cache, required once
`Agrumy.Api` runs more than one replica).

Does **not** cover: the database itself. Point `ConnectionStrings__DefaultConnection` at an external,
already-running MySQL/MariaDB or PostgreSQL instance that can handle concurrent connections from
several `Agrumy.Api` pods - a managed database service, or your own separately-operated
StatefulSet/Service. Running the database itself inside this manifest set (backups, failover, storage
sizing) is a much bigger scope than "add orchestration in front of two stateless apps."

## Prerequisites

1. A Kubernetes cluster with an Ingress controller (ingress-nginx, Traefik, etc.) and, if you want
   the `HorizontalPodAutoscaler` objects to do anything, `metrics-server` (or another
   `metrics.k8s.io` provider) - most managed clusters and common distributions ship one by default.
2. A database reachable from inside the cluster (see above).
3. A StorageClass that supports `ReadWriteMany` if you intend to run more than one `Agrumy.Web`
   replica, or more than one `Agrumy.Api` replica while using the Local firmware-catalog source - see
   `pvc.yaml`'s own remarks for exactly why. Staying at `replicas: 1` for whichever app you can't give
   RWX storage to is a completely valid choice; just don't scale a `ReadWriteOnce` claim past one pod.
4. Your own container images - there is no published registry image yet. Build and push both:
   ```
   docker build -f Agrumy.Api/Dockerfile -t YOUR_REGISTRY/agrumy-api:TAG .
   docker push YOUR_REGISTRY/agrumy-api:TAG
   docker build -f Agrumy.Web/Dockerfile -t YOUR_REGISTRY/agrumy-web:TAG .
   docker push YOUR_REGISTRY/agrumy-web:TAG
   ```
   then edit the `image:` line in `api.yaml`/`web.yaml` to match.

## Apply

```
kubectl apply -f namespace.yaml
kubectl config set-context --current --namespace=agrumy   # optional, saves -n agrumy below

# Secrets - see secret.example.yaml's own header for why this is a create, not an apply of that file
kubectl create secret generic agrumy-secrets -n agrumy \
  --from-literal=ConnectionStrings__DefaultConnection='...' \
  --from-literal=JWT__SecureKey='...'

# TLS cert - either create it yourself, or install cert-manager and uncomment ingress.yaml's annotation
kubectl create secret tls agrumy-tls -n agrumy --cert=fullchain.pem --key=privkey.pem

# Edit configmap.yaml (Database__Provider, JWT__Issuer) and ingress.yaml (host) for your setup first,
# then edit api.yaml/web.yaml's image: lines, then:
kubectl apply -k . -n agrumy
```

Redeploying a new image build:
```
kubectl set image deployment/agrumy-api agrumy-api=YOUR_REGISTRY/agrumy-api:NEW_TAG -n agrumy
kubectl set image deployment/agrumy-web agrumy-web=YOUR_REGISTRY/agrumy-web:NEW_TAG -n agrumy
```
Both Deployments use `maxUnavailable: 0` so a rollout never drops capacity to zero mid-update.

## Bootstrap

Same first-boot flow as any other install - `Agrumy.Api` still needs `ConnectionStrings:DefaultConnection`
set (it is, via the Secret above) before it'll do anything but show the setup wizard, and the first
Global Admin account still comes from the normal bootstrap screen once the app is reachable. There is
no separate Kubernetes-specific bootstrap step.

## Removing

```
kubectl delete -k . -n agrumy
kubectl delete secret agrumy-secrets agrumy-tls -n agrumy
kubectl delete namespace agrumy   # also deletes the PersistentVolumeClaims and whatever they hold
```
