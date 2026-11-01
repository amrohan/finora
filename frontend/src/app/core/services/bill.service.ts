import { Injectable, signal, computed, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable, tap } from 'rxjs';

export type BillFrequency = 1 | 2 | 3 | 4; // 1=Weekly, 2=Monthly, 3=Quarterly, 4=Yearly
export type BillOccurrenceStatus = 1 | 2 | 3 | 4; // 1=Upcoming, 2=Paid, 3=Overdue, 4=Skipped

export interface RecurringBill {
  id: string;
  categoryId?: string;
  categoryName?: string;
  name: string;
  expectedAmountPaise: number;
  frequency: BillFrequency;
  nextDueDate: string;
  autoRecord: boolean;
  isActive: boolean;
  createdAt: string;
}

export interface BillOccurrence {
  id: string;
  billId: string;
  billName: string;
  amountPaise: number;
  dueDate: string;
  status: BillOccurrenceStatus;
  transactionId?: string;
  paidAt?: string;
}

export interface CreateRecurringBillRequest {
  name: string;
  expectedAmountPaise: number;
  frequency: BillFrequency;
  nextDueDate: string;
  categoryId?: string;
  autoRecord?: boolean;
}

export interface PayBillOccurrenceRequest {
  accountId: string;
  paymentDate: string;
  notes?: string;
}

@Injectable({
  providedIn: 'root'
})
export class BillService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = '/api/v1/bills';

  bills = signal<RecurringBill[]>([]);
  occurrences = signal<BillOccurrence[]>([]);
  loading = signal<boolean>(false);

  overdueOccurrences = computed(() => {
    return this.occurrences().filter(o => o.status === 3); // 3=Overdue
  });

  upcomingOccurrences = computed(() => {
    return this.occurrences().filter(o => o.status === 1); // 1=Upcoming
  });

  loadBills(): Observable<RecurringBill[]> {
    this.loading.set(true);
    return this.http.get<RecurringBill[]>(this.baseUrl).pipe(
      tap({
        next: list => {
          this.bills.set(list);
          this.loading.set(false);
        },
        error: () => this.loading.set(false)
      })
    );
  }

  createBill(payload: CreateRecurringBillRequest): Observable<RecurringBill> {
    return this.http.post<RecurringBill>(this.baseUrl, payload).pipe(
      tap(created => {
        this.bills.update(list => [...list, created]);
        this.loadOccurrences().subscribe();
      })
    );
  }

  deleteBill(id: string): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/${id}`).pipe(
      tap(() => {
        this.bills.update(list => list.filter(b => b.id !== id));
        this.loadOccurrences().subscribe();
      })
    );
  }

  loadOccurrences(params: { startDate?: string; endDate?: string; status?: number } = {}): Observable<BillOccurrence[]> {
    this.loading.set(true);
    let httpParams = new HttpParams();
    if (params.startDate) httpParams = httpParams.set('startDate', params.startDate);
    if (params.endDate) httpParams = httpParams.set('endDate', params.endDate);
    if (params.status) httpParams = httpParams.set('status', params.status);

    return this.http.get<BillOccurrence[]>(`${this.baseUrl}/occurrences`, { params: httpParams }).pipe(
      tap({
        next: list => {
          this.occurrences.set(list);
          this.loading.set(false);
        },
        error: () => this.loading.set(false)
      })
    );
  }

  payOccurrence(id: string, payload: PayBillOccurrenceRequest): Observable<BillOccurrence> {
    return this.http.post<BillOccurrence>(`${this.baseUrl}/occurrences/${id}/pay`, payload).pipe(
      tap(updated => {
        this.occurrences.update(list => list.map(o => o.id === id ? updated : o));
      })
    );
  }
}

