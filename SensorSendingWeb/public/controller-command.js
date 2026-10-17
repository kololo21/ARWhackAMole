export function applyCalibration(command,sensors,seen) {
  if(command.kind!=='calibrate'||typeof command.id!=='string'||seen.has(command.id))return false;
  seen.add(command.id);if(seen.size>64)seen.delete(seen.values().next().value);
  return !!sensors.active&&!sensors.simulated&&sensors.calibrate();
}
