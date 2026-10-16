export interface Client {
  id: string;
  name: string;
  balance: number;
  createdAt: string;
}

export interface CreateClientRequest {
  name: string;
  initialBalance: number;
}