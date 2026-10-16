import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { StatementResponse } from '../models/statement.model';

@Injectable({ providedIn: 'root' })
export class StatementService {
  private readonly http = inject(HttpClient);

  getByPeriod(
    clientId: string,
    startDate: string,
    endDate: string,
    page = 1,
    pageSize = 20
  ): Observable<StatementResponse> {
    const params = new HttpParams()
      .set('startDate', startDate)
      .set('endDate', endDate)
      .set('page', page)
      .set('pageSize', pageSize);

    return this.http.get<StatementResponse>(
      `/api/clients/${clientId}/statement`,
      { params }
    );
  }
}