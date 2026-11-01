import { Injectable, signal, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable, tap } from 'rxjs';

export interface Budget {
  id: string;
  categoryId: string;
  categoryName: string;
  categoryIcon?: string;
  categoryColorHex?: string;
  month: number;
  year: number;
  allocatedAmountPaise: number;
  spentAmountPaise: number;
  percentageUsed: number;
  rolloverEnabled: boolean;
}

export interface BudgetSummary {
  month: number;
  year: number;
  totalAllocatedPaise: number;
  totalSpentPaise: number;
  overallPercentageUsed: number;
  budgets: Budget[];
}

export interface SetBudgetRequest {
  categoryId: string;
  month: number;
  year: number;
  allocatedAmountPaise: number;
  rolloverEnabled?: boolean;
}

@Injectable({
  providedIn: 'root'
})
export class BudgetService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = '/api/v1/budgets';

  summary = signal<BudgetSummary | null>(null);
  loading = signal<boolean>(false);

  loadMonthlyBudgets(month?: number, year?: number): Observable<BudgetSummary> {
    this.loading.set(true);
    let params = new HttpParams();
    if (month) params = params.set('month', month);
    if (year) params = params.set('year', year);

    return this.http.get<BudgetSummary>(this.baseUrl, { params }).pipe(
      tap({
        next: s => {
          this.summary.set(s);
          this.loading.set(false);
        },
        error: () => this.loading.set(false)
      })
    );
  }

  setBudget(payload: SetBudgetRequest): Observable<Budget> {
    return this.http.post<Budget>(this.baseUrl, payload).pipe(
      tap(() => {
        // Reload summary after budget change
        this.loadMonthlyBudgets(payload.month, payload.year).subscribe();
      })
    );
  }

  deleteBudget(id: string, month: number, year: number): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/${id}`).pipe(
      tap(() => {
        this.loadMonthlyBudgets(month, year).subscribe();
      })
    );
  }
}

