export const ROLES = ['Employee', 'Manager', 'Dispatcher', 'GisAdmin', 'SystemAdmin'];
export const SAVED_PLACES = {
  office: { name: 'Tel Aviv · Office', lat: 32.0600, lng: 34.7700 },
  station: { name: 'Tel Aviv · Station', lat: 32.0520, lng: 34.7820 },
  client: { name: 'Tel Aviv · Client', lat: 32.0900, lng: 34.8100 },
  haifa: { name: 'Haifa · Office', lat: 32.7900, lng: 35.0100 },
  'haifa-client': { name: 'Haifa · Client', lat: 32.8100, lng: 35.0350 }
};
export const canView = (roles, view) => ({request:true,bookings:true,approvals:roles.includes('Manager'),dispatch:roles.includes('Dispatcher'),zones:roles.includes('GisAdmin'),admin:roles.includes('SystemAdmin')})[view] === true;
export const coordinateLabel = point => point ? `${point.lat.toFixed(4)}° N, ${point.lng.toFixed(4)}° E` : '';
export const statusLabel = status => ({PendingApproval:'Pending approval',Approved:'Approved',Rejected:'Rejected',Assigned:'Taxi assigned',Completed:'Completed',Cancelled:'Cancelled'})[status] ?? status;
export const tripPayload = (pickup,dropoff,note='') => ({pickup:{lat:pickup.lat,lng:pickup.lng},dropoff:{lat:dropoff.lat,lng:dropoff.lng},note});
