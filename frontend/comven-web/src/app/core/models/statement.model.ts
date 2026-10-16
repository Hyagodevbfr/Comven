import { OperationStatus, OperationType } from './operation.model';

export enum TransactionType {
  Debit = 1,
  Refund = 2
}

export interface StatementItem {
  id: string;
  operationId: string;
  operationType: OperationType;
  operationStatus: OperationStatus;
  transactionType: TransactionType;
  amount: number;
  createdAt: string;
}

export interface StatementResponse {
  items: StatementItem[];
  totalItems: number;
  page: number;
  pageSize: number;
}