-- Seeds dummy backend data for frontend testing.
-- This script does not create or modify users, including "Gerald kiss".
-- It is idempotent: it first removes the demo devices and related demo data.
-- If your client is not already connected to the app database, run:
-- USE tlalocai_databse;

START TRANSACTION;

SET @now = UTC_TIMESTAMP(6);
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

INSERT INTO devices_devices
    (Id, Name, Description, ApiKeyHash, IsActive, CreatedAtUtc, LastSeenAtUtc)
VALUES
    (
        @demo_device_1,
        'Tlaloc Norte - Demo',
        'Dispositivo dummy para validar dashboard, telemetria, control y analiticas.',
        '6B5796DD4071AF108FB937DEEF8CB2049240B3811DE43B5724265FE9B9667BC6',
        1,
        DATE_SUB(@now, INTERVAL 5 DAY),
        DATE_SUB(@now, INTERVAL 4 MINUTE)
    ),
    (
        @demo_device_2,
        'Tlaloc Sur - Demo',
        'Dispositivo dummy con actividad mas antigua para validar historicos.',
        '2D76072EF519DCBA0EA17AF51128F03EB4A0583F3CE9E2F31501ED209C27CD2D',
        1,
        DATE_SUB(@now, INTERVAL 4 DAY),
        DATE_SUB(@now, INTERVAL 75 MINUTE)
    );

INSERT INTO devices_sensors
    (Id, DeviceId, Name, Type, GpioPin, IsActive, CreatedAtUtc)
VALUES
    ('30000000-0000-0000-0000-000000000001', @demo_device_1, 'flow_main', 'Flow', 4, 1, DATE_SUB(@now, INTERVAL 5 DAY)),
    ('30000000-0000-0000-0000-000000000002', @demo_device_1, 'level_low', 'Level', 17, 1, DATE_SUB(@now, INTERVAL 5 DAY)),
    ('30000000-0000-0000-0000-000000000003', @demo_device_1, 'level_mid', 'Level', 27, 1, DATE_SUB(@now, INTERVAL 5 DAY)),
    ('30000000-0000-0000-0000-000000000004', @demo_device_1, 'level_high', 'Level', 22, 1, DATE_SUB(@now, INTERVAL 5 DAY)),
    ('30000000-0000-0000-0000-000000000101', @demo_device_2, 'flow_main', 'Flow', 4, 1, DATE_SUB(@now, INTERVAL 4 DAY)),
    ('30000000-0000-0000-0000-000000000102', @demo_device_2, 'level_low', 'Level', 17, 1, DATE_SUB(@now, INTERVAL 4 DAY)),
    ('30000000-0000-0000-0000-000000000103', @demo_device_2, 'level_mid', 'Level', 27, 1, DATE_SUB(@now, INTERVAL 4 DAY)),
    ('30000000-0000-0000-0000-000000000104', @demo_device_2, 'level_high', 'Level', 22, 1, DATE_SUB(@now, INTERVAL 4 DAY));

INSERT INTO devices_actuators
    (Id, DeviceId, Name, Type, GpioPin, ActiveLow, IsActive, CreatedAtUtc)
VALUES
    ('40000000-0000-0000-0000-000000000001', @demo_device_1, 'pump', 'Pump', 5, 0, 1, DATE_SUB(@now, INTERVAL 5 DAY)),
    ('40000000-0000-0000-0000-000000000002', @demo_device_1, 'valve_1', 'Valve', 6, 0, 1, DATE_SUB(@now, INTERVAL 5 DAY)),
    ('40000000-0000-0000-0000-000000000003', @demo_device_1, 'valve_2', 'Valve', 13, 0, 1, DATE_SUB(@now, INTERVAL 5 DAY)),
    ('40000000-0000-0000-0000-000000000004', @demo_device_1, 'valve_3', 'Valve', 19, 0, 1, DATE_SUB(@now, INTERVAL 5 DAY)),
    ('40000000-0000-0000-0000-000000000005', @demo_device_1, 'valve_4', 'Valve', 26, 0, 1, DATE_SUB(@now, INTERVAL 5 DAY)),
    ('40000000-0000-0000-0000-000000000101', @demo_device_2, 'pump', 'Pump', 5, 0, 1, DATE_SUB(@now, INTERVAL 4 DAY)),
    ('40000000-0000-0000-0000-000000000102', @demo_device_2, 'valve_1', 'Valve', 6, 0, 1, DATE_SUB(@now, INTERVAL 4 DAY)),
    ('40000000-0000-0000-0000-000000000103', @demo_device_2, 'valve_2', 'Valve', 13, 0, 1, DATE_SUB(@now, INTERVAL 4 DAY)),
    ('40000000-0000-0000-0000-000000000104', @demo_device_2, 'valve_3', 'Valve', 19, 0, 1, DATE_SUB(@now, INTERVAL 4 DAY)),
    ('40000000-0000-0000-0000-000000000105', @demo_device_2, 'valve_4', 'Valve', 26, 0, 1, DATE_SUB(@now, INTERVAL 4 DAY));

INSERT INTO telemetry_experiments
    (Id, DeviceId, Name, Description, StartedAtUtc, EndedAtUtc, Status, CreatedAtUtc)
VALUES
    (
        @demo_experiment_1,
        @demo_device_1,
        'Prueba de riego demo en curso',
        'Experimento dummy con mediciones recientes para dashboard y analiticas.',
        DATE_SUB(@now, INTERVAL 210 MINUTE),
        NULL,
        'Running',
        DATE_SUB(@now, INTERVAL 215 MINUTE)
    ),
    (
        @demo_experiment_2,
        @demo_device_2,
        'Prueba de historico demo finalizada',
        'Experimento dummy finalizado para validar pantallas de experimentos.',
        DATE_SUB(@now, INTERVAL 300 MINUTE),
        DATE_SUB(@now, INTERVAL 90 MINUTE),
        'Finished',
        DATE_SUB(@now, INTERVAL 305 MINUTE)
    );

DROP TEMPORARY TABLE IF EXISTS demo_measurement_seed;
CREATE TEMPORARY TABLE demo_measurement_seed (
    Id CHAR(36) NOT NULL PRIMARY KEY,
    DeviceId VARCHAR(80) NOT NULL,
    ExperimentId CHAR(36) NULL,
    TimestampUtc DATETIME(6) NOT NULL,
    FlowLpm DECIMAL(12, 4) NOT NULL,
    TotalLiters DECIMAL(14, 4) NOT NULL,
    PumpOn TINYINT(1) NOT NULL,
    LevelLow TINYINT(1) NOT NULL,
    LevelMid TINYINT(1) NOT NULL,
    LevelHigh TINYINT(1) NOT NULL,
    PumpState TINYINT(1) NOT NULL,
    Valve1State TINYINT(1) NOT NULL,
    Valve2State TINYINT(1) NOT NULL,
    Valve3State TINYINT(1) NOT NULL,
    Valve4State TINYINT(1) NOT NULL
);

INSERT INTO demo_measurement_seed
    (Id, DeviceId, ExperimentId, TimestampUtc, FlowLpm, TotalLiters, PumpOn, LevelLow, LevelMid, LevelHigh, PumpState, Valve1State, Valve2State, Valve3State, Valve4State)
VALUES
    ('10000000-0000-0000-0000-000000000001', @demo_device_1, @demo_experiment_1, DATE_SUB(@now, INTERVAL 225 MINUTE), 0.0000, 1250.0000, 0, 1, 0, 0, 0, 0, 0, 0, 0),
    ('10000000-0000-0000-0000-000000000002', @demo_device_1, @demo_experiment_1, DATE_SUB(@now, INTERVAL 210 MINUTE), 4.2000, 1254.8000, 1, 1, 0, 0, 1, 1, 0, 0, 0),
    ('10000000-0000-0000-0000-000000000003', @demo_device_1, @demo_experiment_1, DATE_SUB(@now, INTERVAL 195 MINUTE), 6.9000, 1262.3000, 1, 1, 1, 0, 1, 1, 0, 0, 0),
    ('10000000-0000-0000-0000-000000000004', @demo_device_1, @demo_experiment_1, DATE_SUB(@now, INTERVAL 180 MINUTE), 8.4000, 1271.1000, 1, 1, 1, 0, 1, 1, 1, 0, 0),
    ('10000000-0000-0000-0000-000000000005', @demo_device_1, @demo_experiment_1, DATE_SUB(@now, INTERVAL 165 MINUTE), 7.7000, 1279.4000, 1, 1, 1, 0, 1, 0, 1, 0, 0),
    ('10000000-0000-0000-0000-000000000006', @demo_device_1, @demo_experiment_1, DATE_SUB(@now, INTERVAL 150 MINUTE), 9.1000, 1289.2000, 1, 1, 1, 1, 1, 0, 1, 0, 0),
    ('10000000-0000-0000-0000-000000000007', @demo_device_1, @demo_experiment_1, DATE_SUB(@now, INTERVAL 135 MINUTE), 10.3000, 1300.7000, 1, 1, 1, 1, 1, 0, 1, 1, 0),
    ('10000000-0000-0000-0000-000000000008', @demo_device_1, @demo_experiment_1, DATE_SUB(@now, INTERVAL 120 MINUTE), 6.1000, 1307.5000, 1, 1, 1, 1, 1, 0, 0, 1, 0),
    ('10000000-0000-0000-0000-000000000009', @demo_device_1, @demo_experiment_1, DATE_SUB(@now, INTERVAL 105 MINUTE), 2.3000, 1309.9000, 1, 1, 1, 0, 1, 0, 0, 1, 0),
    ('10000000-0000-0000-0000-000000000010', @demo_device_1, @demo_experiment_1, DATE_SUB(@now, INTERVAL 90 MINUTE), 0.0000, 1309.9000, 0, 1, 1, 0, 0, 0, 0, 0, 0),
    ('10000000-0000-0000-0000-000000000011', @demo_device_1, @demo_experiment_1, DATE_SUB(@now, INTERVAL 75 MINUTE), 5.8000, 1316.1000, 1, 1, 1, 0, 1, 1, 0, 0, 1),
    ('10000000-0000-0000-0000-000000000012', @demo_device_1, @demo_experiment_1, DATE_SUB(@now, INTERVAL 60 MINUTE), 8.9000, 1325.3000, 1, 1, 1, 1, 1, 1, 0, 0, 1),
    ('10000000-0000-0000-0000-000000000013', @demo_device_1, @demo_experiment_1, DATE_SUB(@now, INTERVAL 45 MINUTE), 9.6000, 1335.4000, 1, 1, 1, 1, 1, 0, 1, 0, 1),
    ('10000000-0000-0000-0000-000000000014', @demo_device_1, @demo_experiment_1, DATE_SUB(@now, INTERVAL 30 MINUTE), 4.5000, 1340.2000, 1, 1, 1, 0, 1, 0, 1, 0, 0),
    ('10000000-0000-0000-0000-000000000015', @demo_device_1, @demo_experiment_1, DATE_SUB(@now, INTERVAL 15 MINUTE), 1.2000, 1341.5000, 1, 1, 0, 0, 1, 0, 0, 0, 0),
    ('10000000-0000-0000-0000-000000000016', @demo_device_1, @demo_experiment_1, DATE_SUB(@now, INTERVAL 5 MINUTE), 0.0000, 1341.5000, 0, 1, 0, 0, 0, 0, 0, 0, 0),
    ('10000000-0000-0000-0000-000000000101', @demo_device_2, @demo_experiment_2, DATE_SUB(@now, INTERVAL 300 MINUTE), 0.0000, 870.0000, 0, 1, 0, 0, 0, 0, 0, 0, 0),
    ('10000000-0000-0000-0000-000000000102', @demo_device_2, @demo_experiment_2, DATE_SUB(@now, INTERVAL 270 MINUTE), 3.3000, 875.0000, 1, 1, 0, 0, 1, 1, 0, 0, 0),
    ('10000000-0000-0000-0000-000000000103', @demo_device_2, @demo_experiment_2, DATE_SUB(@now, INTERVAL 240 MINUTE), 5.6000, 883.4000, 1, 1, 1, 0, 1, 1, 0, 0, 0),
    ('10000000-0000-0000-0000-000000000104', @demo_device_2, @demo_experiment_2, DATE_SUB(@now, INTERVAL 210 MINUTE), 7.2000, 894.2000, 1, 1, 1, 0, 1, 0, 1, 0, 0),
    ('10000000-0000-0000-0000-000000000105', @demo_device_2, @demo_experiment_2, DATE_SUB(@now, INTERVAL 180 MINUTE), 6.4000, 903.9000, 1, 1, 1, 1, 1, 0, 1, 0, 0),
    ('10000000-0000-0000-0000-000000000106', @demo_device_2, @demo_experiment_2, DATE_SUB(@now, INTERVAL 150 MINUTE), 2.1000, 907.2000, 1, 1, 1, 0, 1, 0, 0, 1, 0),
    ('10000000-0000-0000-0000-000000000107', @demo_device_2, @demo_experiment_2, DATE_SUB(@now, INTERVAL 120 MINUTE), 0.0000, 907.2000, 0, 1, 0, 0, 0, 0, 0, 0, 0),
    ('10000000-0000-0000-0000-000000000108', @demo_device_2, @demo_experiment_2, DATE_SUB(@now, INTERVAL 90 MINUTE), 0.0000, 907.2000, 0, 1, 0, 0, 0, 0, 0, 0, 0);

INSERT INTO telemetry_measurements
    (Id, DeviceId, ExperimentId, TimestampUtc, FlowLpm, TotalLiters, PumpOn, CreatedAtUtc)
SELECT
    Id,
    DeviceId,
    ExperimentId,
    TimestampUtc,
    FlowLpm,
    TotalLiters,
    PumpOn,
    @now
FROM demo_measurement_seed;

INSERT INTO telemetry_level_measurements
    (Id, MeasurementId, SensorName, IsActive)
SELECT UUID(), Id, 'level_low', LevelLow FROM demo_measurement_seed;

INSERT INTO telemetry_level_measurements
    (Id, MeasurementId, SensorName, IsActive)
SELECT UUID(), Id, 'level_mid', LevelMid FROM demo_measurement_seed;

INSERT INTO telemetry_level_measurements
    (Id, MeasurementId, SensorName, IsActive)
SELECT UUID(), Id, 'level_high', LevelHigh FROM demo_measurement_seed;

INSERT INTO telemetry_actuator_snapshots
    (Id, MeasurementId, ActuatorName, IsOn)
SELECT UUID(), Id, 'pump', PumpState FROM demo_measurement_seed;

INSERT INTO telemetry_actuator_snapshots
    (Id, MeasurementId, ActuatorName, IsOn)
SELECT UUID(), Id, 'valve_1', Valve1State FROM demo_measurement_seed;

INSERT INTO telemetry_actuator_snapshots
    (Id, MeasurementId, ActuatorName, IsOn)
SELECT UUID(), Id, 'valve_2', Valve2State FROM demo_measurement_seed;

INSERT INTO telemetry_actuator_snapshots
    (Id, MeasurementId, ActuatorName, IsOn)
SELECT UUID(), Id, 'valve_3', Valve3State FROM demo_measurement_seed;

INSERT INTO telemetry_actuator_snapshots
    (Id, MeasurementId, ActuatorName, IsOn)
SELECT UUID(), Id, 'valve_4', Valve4State FROM demo_measurement_seed;

INSERT INTO control_commands
    (Id, DeviceId, Type, Target, State, Status, CreatedAtUtc, SentAtUtc, ExecutedAtUtc, ErrorMessage)
VALUES
    (
        '50000000-0000-0000-0000-000000000001',
        @demo_device_1,
        'SetActuatorState',
        'pump',
        1,
        'Executed',
        DATE_SUB(@now, INTERVAL 185 MINUTE),
        DATE_SUB(@now, INTERVAL 184 MINUTE),
        DATE_SUB(@now, INTERVAL 183 MINUTE),
        NULL
    ),
    (
        '50000000-0000-0000-0000-000000000002',
        @demo_device_1,
        'SetActuatorState',
        'valve_2',
        1,
        'Executed',
        DATE_SUB(@now, INTERVAL 178 MINUTE),
        DATE_SUB(@now, INTERVAL 177 MINUTE),
        DATE_SUB(@now, INTERVAL 176 MINUTE),
        NULL
    ),
    (
        '50000000-0000-0000-0000-000000000003',
        @demo_device_1,
        'SetActuatorState',
        'valve_3',
        1,
        'Failed',
        DATE_SUB(@now, INTERVAL 132 MINUTE),
        DATE_SUB(@now, INTERVAL 131 MINUTE),
        DATE_SUB(@now, INTERVAL 130 MINUTE),
        'Demo: la valvula no confirmo apertura.'
    ),
    (
        '50000000-0000-0000-0000-000000000004',
        @demo_device_1,
        'SetActuatorState',
        'valve_1',
        0,
        'Pending',
        DATE_SUB(@now, INTERVAL 3 MINUTE),
        NULL,
        NULL,
        NULL
    ),
    (
        '50000000-0000-0000-0000-000000000101',
        @demo_device_2,
        'SetActuatorState',
        'pump',
        1,
        'Executed',
        DATE_SUB(@now, INTERVAL 268 MINUTE),
        DATE_SUB(@now, INTERVAL 267 MINUTE),
        DATE_SUB(@now, INTERVAL 266 MINUTE),
        NULL
    ),
    (
        '50000000-0000-0000-0000-000000000102',
        @demo_device_2,
        'SetActuatorState',
        'valve_4',
        0,
        'Cancelled',
        DATE_SUB(@now, INTERVAL 88 MINUTE),
        NULL,
        NULL,
        NULL
    );

DROP TEMPORARY TABLE IF EXISTS demo_measurement_seed;

COMMIT;

-- Optional device API keys for hardware endpoint simulation:
-- demo-tlaloc-norte: tlaloc_demo_key_norte
-- demo-tlaloc-sur: tlaloc_demo_key_sur
