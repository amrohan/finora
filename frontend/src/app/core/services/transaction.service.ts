import { Injectable, signal, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable, tap } from 'rxjs';

export interface Category {
  id: string;
  parentCategoryId?: string;
  name: string;
  type: number | string;
  icon?: string;
  colorHex?: string;
  isSystem: boolean;
  subCategories?: Category[];
}

export interface TransactionSplit {
  categoryId: string;
  categoryName?: string;
  amountPaise: number;
  notes?: string;
}

export interface Transaction {
  id: string;
  accountId: string;
  accountName: string;
  categoryId?: string;
  categoryName?: string;
  transactionType: number; // 1=Expense, 2=Income, 3=Transfer
  amountPaise: number;
  date: string;
  payee?: string;
  notes?: string;
  visibility: number;
  isSplit: boolean;
  splits?: TransactionSplit[];
  createdAt: string;
}

export interface PagedResult<T> {
  items: T[];
  totalCount: number;
  page: number;
  pageSize: number;
  totalPages: number;
  hasPreviousPage: boolean;
  hasNextPage: boolean;
}

export interface CreateTransactionRequest {
  accountId: string;
  categoryId?: string;
  transactionType: number;
  amountPaise: number;
  date: string;
  payee?: string;
  notes?: string;
  visibility?: number;
  splits?: { categoryId: string; amountPaise: number; notes?: string }[];
}

export interface RecordTransferRequest {
  sourceAccountId: string;
  destinationAccountId: string;
  amountPaise: number;
  date: string;
  notes?: string;
}

export interface ParsedFileResult {
  fileName: string;
  totalRows: number;
  columns: string[];
  previewRows: Record<string, string>[];
  allRows: Record<string, string>[];
}

export interface ImportTransactionItem {
  date: string;
  payee?: string;
  amountPaise: number;
  transactionType: number;
  categoryId?: string;
  notes?: string;
  visibility?: number;
}

export interface ExecuteImportRequest {
  accountId: string;
  transactions: ImportTransactionItem[];
  skipDuplicates: boolean;
}

export interface ExecuteImportResponse {
  importedCount: number;
  skippedCount: number;
  newAccountBalancePaise: number;
}

@Injectable({
  providedIn: 'root'
})
export class TransactionService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = '/api/v1/transactions';
  private readonly categoriesUrl = '/api/v1/categories';

  transactions = signal<Transaction[]>([]);
  totalCount = signal<number>(0);
  loading = signal<boolean>(false);
  categories = signal<Category[]>([]);

  loadTransactions(params: {
    page?: number;
    pageSize?: number;
    startDate?: string;
    endDate?: string;
    accountId?: string;
    categoryId?: string;
    type?: number;
    search?: string;
  } = {}): Observable<PagedResult<Transaction>> {
    this.loading.set(true);
    let httpParams = new HttpParams();
    if (params.page) httpParams = httpParams.set('page', params.page);
    if (params.pageSize) httpParams = httpParams.set('pageSize', params.pageSize);
    if (params.startDate) httpParams = httpParams.set('startDate', params.startDate);
    if (params.endDate) httpParams = httpParams.set('endDate', params.endDate);
    if (params.accountId) httpParams = httpParams.set('accountId', params.accountId);
    if (params.categoryId) httpParams = httpParams.set('categoryId', params.categoryId);
    if (params.type) httpParams = httpParams.set('type', params.type);
    if (params.search) httpParams = httpParams.set('search', params.search.trim());

    return this.http.get<PagedResult<Transaction>>(this.baseUrl, { params: httpParams }).pipe(
      tap({
        next: res => {
          this.transactions.set(res.items);
          this.totalCount.set(res.totalCount);
          this.loading.set(false);
        },
        error: () => this.loading.set(false)
      })
    );
  }

  createTransaction(payload: CreateTransactionRequest): Observable<Transaction> {
    return this.http.post<Transaction>(this.baseUrl, payload).pipe(
      tap(created => {
        this.transactions.update(list => [created, ...list]);
        this.totalCount.update(c => c + 1);
      })
    );
  }

  recordTransfer(payload: RecordTransferRequest): Observable<any> {
    return this.http.post<any>(`${this.baseUrl}/transfers`, payload);
  }

  deleteTransaction(id: string): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/${id}`).pipe(
      tap(() => {
        this.transactions.update(list => list.filter(t => t.id !== id));
        this.totalCount.update(c => Math.max(0, c - 1));
      })
    );
  }

  loadCategories(type?: string): Observable<Category[]> {
    let params = new HttpParams();
    if (type) params = params.set('type', type);
    return this.http.get<Category[]>(this.categoriesUrl, { params }).pipe(
      tap(cats => this.categories.set(cats))
    );
  }

  parseImportFile(file: File): Observable<ParsedFileResult> {
    const formData = new FormData();
    formData.append('file', file, file.name);
    return this.http.post<ParsedFileResult>(`${this.baseUrl}/import/parse`, formData);
  }

  executeImport(payload: ExecuteImportRequest): Observable<ExecuteImportResponse> {
    return this.http.post<ExecuteImportResponse>(`${this.baseUrl}/import/execute`, payload);
  }

  bulkCategorize(transactionIds: string[], categoryId: string | null): Observable<{ affectedCount: number }> {
    return this.http.post<{ affectedCount: number }>(`${this.baseUrl}/bulk-categorize`, {
      transactionIds,
      categoryId
    });
  }

  bulkDelete(transactionIds: string[]): Observable<{ affectedCount: number }> {
    return this.http.post<{ affectedCount: number }>(`${this.baseUrl}/bulk-delete`, {
      transactionIds
    });
  }

  bulkVisibility(transactionIds: string[], visibility: number): Observable<{ affectedCount: number }> {
    return this.http.post<{ affectedCount: number }>(`${this.baseUrl}/bulk-visibility`, {
      transactionIds,
      visibility
    });
  }
}

