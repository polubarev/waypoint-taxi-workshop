import { cp, mkdir } from 'node:fs/promises';
await mkdir('web/vendor',{recursive:true});
await cp('node_modules/leaflet/dist','web/vendor/leaflet',{recursive:true});
await cp('node_modules/leaflet-draw/dist','web/vendor/leaflet-draw',{recursive:true});
