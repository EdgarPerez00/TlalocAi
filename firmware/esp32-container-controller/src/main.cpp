#include <Arduino.h>

#ifndef BOARD_ID
#define BOARD_ID "esp32-a"
#endif

struct ContainerConfig {
  uint8_t sensorA;
  uint8_t sensorB;
  uint8_t statusOutput;
};

struct ValveConfig {
  uint8_t outputPin;
  uint8_t containerIndexA;
  uint8_t containerIndexB;
};

const ContainerConfig containers[4] = {
  {32, 33, 16},
  {34, 35, 17},
  {36, 39, 18},
  {27, 14, 19}
};

const ValveConfig valves[2] = {
  {25, 0, 1},
  {26, 2, 3}
};

bool containerFull[4] = {false, false, false, false};
bool valveOpen[2] = {false, false};
bool valveLocked[2] = {false, false};

unsigned long lastStatusMs = 0;
const unsigned long statusIntervalMs = 1000;

bool readDigitalSafe(uint8_t pin) {
  return digitalRead(pin) == HIGH;
}

// Required truth table:
// 00 -> 0
// 01 -> 0
// 10 -> 1
// 11 -> 1
// sensorA is the dominant/full sensor.
bool evaluateContainerFull(bool sensorA, bool sensorB) {
  if (!sensorA && !sensorB) return false;
  if (!sensorA && sensorB) return false;
  if (sensorA && !sensorB) return true;
  return true;
}

void applyValveOutput(uint8_t valveIndex) {
  digitalWrite(valves[valveIndex].outputPin, valveOpen[valveIndex] ? HIGH : LOW);
}

void closeValve(uint8_t valveIndex) {
  valveOpen[valveIndex] = false;
  applyValveOutput(valveIndex);
}

bool canOpenValve(uint8_t valveIndex) {
  if (valveLocked[valveIndex]) return false;

  uint8_t a = valves[valveIndex].containerIndexA;
  uint8_t b = valves[valveIndex].containerIndexB;

  if (containerFull[a] || containerFull[b]) return false;

  return true;
}

bool openValve(uint8_t valveIndex) {
  if (!canOpenValve(valveIndex)) {
    closeValve(valveIndex);
    return false;
  }

  valveOpen[valveIndex] = true;
  applyValveOutput(valveIndex);
  return true;
}

void updateContainers() {
  for (uint8_t i = 0; i < 4; i++) {
    bool sensorA = readDigitalSafe(containers[i].sensorA);
    bool sensorB = readDigitalSafe(containers[i].sensorB);

    containerFull[i] = evaluateContainerFull(sensorA, sensorB);

    digitalWrite(containers[i].statusOutput, containerFull[i] ? HIGH : LOW);
  }
}

void updateValveSafety() {
  for (uint8_t i = 0; i < 2; i++) {
    uint8_t a = valves[i].containerIndexA;
    uint8_t b = valves[i].containerIndexB;

    bool anyFull = containerFull[a] || containerFull[b];
    bool bothEmpty = !containerFull[a] && !containerFull[b];

    if (anyFull) {
      valveLocked[i] = true;
      closeValve(i);
    }

    if (bothEmpty) {
      valveLocked[i] = false;
    }

    if (valveOpen[i] && !canOpenValve(i)) {
      closeValve(i);
    }
  }
}

void printStatus() {
  Serial.print("{\"boardId\":\"");
  Serial.print(BOARD_ID);
  Serial.print("\",\"containers\":[");

  for (uint8_t i = 0; i < 4; i++) {
    Serial.print(containerFull[i] ? "1" : "0");
    if (i < 3) Serial.print(",");
  }

  Serial.print("],\"valves\":[");
  for (uint8_t i = 0; i < 2; i++) {
    Serial.print("{\"index\":");
    Serial.print(i + 1);
    Serial.print(",\"open\":");
    Serial.print(valveOpen[i] ? "true" : "false");
    Serial.print(",\"locked\":");
    Serial.print(valveLocked[i] ? "true" : "false");
    Serial.print("}");

    if (i < 1) Serial.print(",");
  }

  Serial.println("]}");
}

void processCommand(String command) {
  command.trim();
  command.toUpperCase();

  if (command == "STATUS") {
    printStatus();
    return;
  }

  if (command.startsWith("OPEN ")) {
    int valveNumber = command.substring(5).toInt();

    if (valveNumber < 1 || valveNumber > 2) {
      Serial.println("{\"ok\":false,\"error\":\"INVALID_VALVE\"}");
      return;
    }

    bool opened = openValve((uint8_t)(valveNumber - 1));

    if (opened) {
      Serial.println("{\"ok\":true,\"action\":\"OPEN\"}");
    } else {
      Serial.println("{\"ok\":false,\"error\":\"VALVE_LOCKED_OR_CONTAINER_FULL\"}");
    }

    return;
  }

  if (command.startsWith("CLOSE ")) {
    int valveNumber = command.substring(6).toInt();

    if (valveNumber < 1 || valveNumber > 2) {
      Serial.println("{\"ok\":false,\"error\":\"INVALID_VALVE\"}");
      return;
    }

    closeValve((uint8_t)(valveNumber - 1));
    Serial.println("{\"ok\":true,\"action\":\"CLOSE\"}");
    return;
  }

  Serial.println("{\"ok\":false,\"error\":\"UNKNOWN_COMMAND\"}");
}

void setup() {
  Serial.begin(115200);

  for (uint8_t i = 0; i < 4; i++) {
    pinMode(containers[i].sensorA, INPUT);
    pinMode(containers[i].sensorB, INPUT);
    pinMode(containers[i].statusOutput, OUTPUT);
    digitalWrite(containers[i].statusOutput, LOW);
  }

  for (uint8_t i = 0; i < 2; i++) {
    pinMode(valves[i].outputPin, OUTPUT);
    closeValve(i);
  }

  Serial.print("{\"boardId\":\"");
  Serial.print(BOARD_ID);
  Serial.println("\",\"status\":\"BOOT\"}");
}

void loop() {
  updateContainers();
  updateValveSafety();

  while (Serial.available() > 0) {
    String command = Serial.readStringUntil('\n');
    processCommand(command);
  }

  unsigned long now = millis();
  if (now - lastStatusMs >= statusIntervalMs) {
    lastStatusMs = now;
    printStatus();
  }

  delay(50);
}
