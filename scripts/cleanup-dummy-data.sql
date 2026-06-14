-- Cleans only the dummy data created by scripts/seed-dummy-data.sql.
-- If your client is not already connected to the app database, run:
-- USE tlalocai_databse;

START TRANSACTION;

SET @demo_device_1 = 'demo-tlaloc-norte';
SET @demo_device_2 = 'demo-tlaloc-sur';
SET @demo_experiment_1 = '20000000-0000-0000-0000-000000000001';
SET @demo_experiment_2 = '20000000-0000-0000-0000-000000000002';

DELETE FROM telemetry_actuator_snapshots
WHERE MeasurementId IN (
    SELECT Id
    FROM telemetry_measurements
    WHERE DeviceId IN (@demo_device_1, @demo_device_2)
);

DELETE FROM telemetry_level_measurements
WHERE MeasurementId IN (
    SELECT Id
    FROM telemetry_measurements
    WHERE DeviceId IN (@demo_device_1, @demo_device_2)
);

DELETE FROM telemetry_measurements
WHERE DeviceId IN (@demo_device_1, @demo_device_2);

DELETE FROM telemetry_experiments
WHERE DeviceId IN (@demo_device_1, @demo_device_2)
   OR Id IN (@demo_experiment_1, @demo_experiment_2);

DELETE FROM control_commands
WHERE DeviceId IN (@demo_device_1, @demo_device_2);

DELETE FROM devices_actuators
WHERE DeviceId IN (@demo_device_1, @demo_device_2);

DELETE FROM devices_sensors
WHERE DeviceId IN (@demo_device_1, @demo_device_2);

DELETE FROM devices_devices
WHERE Id IN (@demo_device_1, @demo_device_2);

COMMIT;
