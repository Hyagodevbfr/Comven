export enum OperationType {
  VehicleConsultation = 1,
  DocumentIssuance = 2,
  LicensePlateConsultation = 3
}

export enum OperationStatus {
  Pending = 1,
  Completed = 2,
  Failed = 3
}

export interface CreateOperationRequest {
  clientId: string;
  idempotencyKey: string;
  type: OperationType;
}

export interface Operation {
  id: string;
  clientId: string;
  type: OperationType;
  amount: number;
  status: OperationStatus;
  createdAt: string;
  finishedAt: string | null;
}