import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import {
  CreateOperationRequest,
  Operation
} from '../models/operation.model';

@Injectable({ providedIn: 'root' })
export class OperationService {
  private readonly http = inject(HttpClient);
  private readonly apiUrl = '/api/operations';

  create(request: CreateOperationRequest): Observable<Operation> {
    return this.http.post<Operation>(this.apiUrl, request);
  }

  getById(id: string): Observable<Operation> {
    return this.http.get<Operation>(`${this.apiUrl}/${id}`);
  }
}