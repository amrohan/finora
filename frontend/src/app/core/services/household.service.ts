import { Injectable, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, tap } from 'rxjs';

export interface Household {
  id: string;
  name: string;
  currency: string;
  timezone: string;
  role: string;
  memberCount: number;
  createdAt: string;
}

export interface HouseholdMember {
  userId: string;
  fullName: string;
  email: string;
  role: string;
  joinedAt: string;
}

export interface Invitation {
  id: string;
  email: string;
  role: string;
  status: string;
  expiresAt: string;
  createdAt: string;
  inviteToken?: string;
}

@Injectable({
  providedIn: 'root'
})
export class HouseholdService {
  private readonly baseUrl = '/api/v1/households';

  households = signal<Household[]>([]);
  activeHousehold = signal<Household | null>(null);

  constructor(private http: HttpClient) {}

  getUserHouseholds(): Observable<Household[]> {
    return this.http.get<Household[]>(this.baseUrl).pipe(
      tap(list => {
        this.households.set(list);
        if (list.length > 0 && !this.activeHousehold()) {
          this.activeHousehold.set(list[0]);
        }
      })
    );
  }

  createHousehold(payload: { name: string; currency?: string; timezone?: string }): Observable<Household> {
    return this.http.post<Household>(this.baseUrl, payload).pipe(
      tap(created => {
        this.households.update(list => [...list, created]);
        this.activeHousehold.set(created);
      })
    );
  }

  getHousehold(id: string): Observable<Household> {
    return this.http.get<Household>(`${this.baseUrl}/${id}`);
  }

  getMembers(id: string): Observable<HouseholdMember[]> {
    return this.http.get<HouseholdMember[]>(`${this.baseUrl}/${id}/members`);
  }

  inviteMember(householdId: string, payload: { email: string; role: string }): Observable<Invitation> {
    return this.http.post<Invitation>(`${this.baseUrl}/${householdId}/invitations`, payload);
  }

  inspectInvitation(token: string): Observable<any> {
    return this.http.get<any>(`${this.baseUrl}/invitations/${token}`);
  }
}

