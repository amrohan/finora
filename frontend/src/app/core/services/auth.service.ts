import { Injectable, signal, computed } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, tap } from 'rxjs';

export interface User {
  id: string;
  email: string;
  fullName: string;
  avatarUrl?: string;
  activeHouseholdId?: string;
  role?: string;
}

export interface AuthResponse {
  accessToken: string;
  refreshToken: string;
  expiresInSeconds: number;
  userId: string;
  email: string;
  fullName: string;
  activeHouseholdId?: string;
  role?: string;
}

@Injectable({
  providedIn: 'root'
})
export class AuthService {
  private readonly baseUrl = '/api/v1/auth';
  private readonly TOKEN_KEY = 'finora_access_token';
  private readonly REFRESH_KEY = 'finora_refresh_token';
  private readonly USER_KEY = 'finora_user';
  private readonly HOUSEHOLD_KEY = 'finora_active_household_id';

  currentUser = signal<User | null>(this.getStoredUser());
  token = signal<string | null>(localStorage.getItem(this.TOKEN_KEY));
  activeHouseholdId = signal<string | null>(localStorage.getItem(this.HOUSEHOLD_KEY));

  isAuthenticated = computed(() => !!this.token() && !!this.currentUser());

  constructor(private http: HttpClient) {}

  registerInitialAdmin(payload: any): Observable<AuthResponse> {
    return this.http.post<AuthResponse>(`${this.baseUrl}/register-initial`, payload).pipe(
      tap(res => this.handleAuthSuccess(res))
    );
  }

  registerWithInvite(payload: any): Observable<AuthResponse> {
    return this.http.post<AuthResponse>(`${this.baseUrl}/register-with-invite`, payload).pipe(
      tap(res => this.handleAuthSuccess(res))
    );
  }

  login(payload: any): Observable<AuthResponse> {
    return this.http.post<AuthResponse>(`${this.baseUrl}/login`, payload).pipe(
      tap(res => this.handleAuthSuccess(res))
    );
  }

  logout(): void {
    localStorage.removeItem(this.TOKEN_KEY);
    localStorage.removeItem(this.REFRESH_KEY);
    localStorage.removeItem(this.USER_KEY);
    localStorage.removeItem(this.HOUSEHOLD_KEY);

    this.currentUser.set(null);
    this.token.set(null);
    this.activeHouseholdId.set(null);
  }

  setActiveHousehold(householdId: string): void {
    localStorage.setItem(this.HOUSEHOLD_KEY, householdId);
    this.activeHouseholdId.set(householdId);
  }

  private handleAuthSuccess(res: AuthResponse): void {
    localStorage.setItem(this.TOKEN_KEY, res.accessToken);
    localStorage.setItem(this.REFRESH_KEY, res.refreshToken);

    const user: User = {
      id: res.userId,
      email: res.email,
      fullName: res.fullName,
      activeHouseholdId: res.activeHouseholdId,
      role: res.role
    };

    localStorage.setItem(this.USER_KEY, JSON.stringify(user));
    this.currentUser.set(user);
    this.token.set(res.accessToken);

    if (res.activeHouseholdId) {
      this.setActiveHousehold(res.activeHouseholdId);
    }
  }

  private getStoredUser(): User | null {
    const raw = localStorage.getItem(this.USER_KEY);
    if (!raw) return null;
    try {
      return JSON.parse(raw) as User;
    } catch {
      return null;
    }
  }
}

