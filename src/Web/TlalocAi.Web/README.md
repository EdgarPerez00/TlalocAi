# TlalocAi.Web

Frontend web para `TlalocAi.Platform`, orientado a monitoreo de dispositivos, telemetría, control de actuadores y analítica del sistema de medición de agua.

## Requisitos

- Node.js 20+
- npm 10+
- Backend `TlalocAi.Platform` disponible localmente
- Docker opcional para build y despliegue del frontend

## Instalación

```bash
npm install
```

## Configuración

1. Copia `.env.example` a `.env`.
2. Ajusta las URLs `VITE_*` según tu entorno.

Variables incluidas:

- `VITE_API_BASE_URL=http://localhost:5100`
- `VITE_IDENTITY_API_BASE_URL=http://localhost:5101`
- `VITE_DEVICES_API_BASE_URL=http://localhost:5102`
- `VITE_TELEMETRY_API_BASE_URL=http://localhost:5103`
- `VITE_CONTROL_API_BASE_URL=http://localhost:5104`
- `VITE_ANALYTICS_API_BASE_URL=http://localhost:5105`
- `VITE_USE_GATEWAY=true`

Regla de integración:

- Si `VITE_USE_GATEWAY=true`, todos los módulos API consumen `VITE_API_BASE_URL`.
- Si `VITE_USE_GATEWAY=false`, cada módulo usa su URL específica.

## Ejecución local

```bash
npm run dev
```

El frontend queda disponible en `http://localhost:5173`.

## Build y preview

```bash
npm run build
npm run preview
```

`preview` expone el build en `http://localhost:4173`.

## Docker

Build local:

```bash
docker build -t tlalocai-web .
```

Ejecución:

```bash
docker run --rm -p 5174:80 --name tlalocai-web tlalocai-web
```

El contenedor publica la SPA en `http://localhost:5174`.

## Integración con TlalocAi.Platform

El flujo preferente es consumir el Gateway:

- Gateway: `http://localhost:5100`
- Identity: `http://localhost:5101`
- Devices: `http://localhost:5102`
- Telemetry: `http://localhost:5103`
- Control: `http://localhost:5104`
- Analytics: `http://localhost:5105`

Para el `docker-compose.yml` raíz se agregó el servicio `tlalocai-web` con build desde `src/Web/TlalocAi.Web` y puerto host `5174:80`.

## Variables Vite en producción

Vite inyecta `VITE_*` en build time. Para cambiar `VITE_API_BASE_URL` en producción necesitas:

- reconstruir la imagen, o
- implementar una estrategia de runtime config aparte.

Para este MVP se usa build-time env.

## Flujo de uso

- Login: inicia sesión o registra un usuario desde `/login`.
- Dashboard: revisa última medición, flujo, litros, bomba, válvulas y niveles.
- Control: envía comandos `SetActuatorState` para bomba y `valve_1` a `valve_4`.
- Estadísticas: consulta series de caudal y resúmenes analíticos por rango.

## Puertos usados

- Frontend dev: `5173`
- Frontend preview: `4173`
- Frontend Docker: `5174`
- Gateway backend: `5100`
