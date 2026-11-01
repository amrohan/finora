import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';

export interface MonthlyCashFlow {
  month: number;
  year: number;
  inflowPaise: number;
  outflowPaise: number;
  netCashFlowPaise: number;
}

export interface NetWorthOverview {
  totalAssetsPaise: number;
  totalLiabilitiesPaise: number;
  netWorthPaise: number;
  currentMonthInflowPaise: number;
  currentMonthOutflowPaise: number;
  savingsRatePercentage: number;
  cashFlowHistory: MonthlyCashFlow[];
}

export interface CategorySpending {
  categoryId?: string;
  categoryName: string;
  icon?: string;
  colorHex?: string;
  amountPaise: number;
  percentage: number;
}

export interface MerchantSpending {
  payee: string;
  amountPaise: number;
  transactionCount: number;
}

export interface SpendingBreakdown {
  month: number;
  year: number;
  totalExpensePaise: number;
  categories: CategorySpending[];
  topPayees: MerchantSpending[];
}

@Injectable({
  providedIn: 'root'
})
export class AnalyticsService {
  private http = inject(HttpClient);
  private apiUrl = '/api/v1/analytics';

  getOverview(months: number = 6): Observable<NetWorthOverview> {
    return this.http.get<NetWorthOverview>(`${this.apiUrl}/overview`, {
      params: new HttpParams().set('months', months.toString())
    });
  }

  getSpendingBreakdown(month?: number, year?: number): Observable<SpendingBreakdown> {
    let params = new HttpParams();
    if (month) params = params.set('month', month.toString());
    if (year) params = params.set('year', year.toString());
    return this.http.get<SpendingBreakdown>(`${this.apiUrl}/spending-breakdown`, { params });
  }

  exportTransactions(
    format: 'csv' | 'json' = 'csv',
    filters?: { startDate?: string; endDate?: string; accountId?: string; categoryId?: string }
  ): Observable<Blob> {
    let params = new HttpParams().set('format', format);
    if (filters?.startDate) params = params.set('startDate', filters.startDate);
    if (filters?.endDate) params = params.set('endDate', filters.endDate);
    if (filters?.accountId) params = params.set('accountId', filters.accountId);
    if (filters?.categoryId) params = params.set('categoryId', filters.categoryId);

    return this.http.get(`${this.apiUrl}/export/transactions`, {
      params,
      responseType: 'blob'
    });
  }

  exportAccounts(format: 'csv' | 'json' = 'csv'): Observable<Blob> {
    return this.http.get(`${this.apiUrl}/export/accounts`, {
      params: new HttpParams().set('format', format),
      responseType: 'blob'
    });
  }
}

