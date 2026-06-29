import { inject, Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { environment } from '../../environments/environment';
import { AiApplication, AiApplicationRequest, ChatRequest, ChatResponse, ChatHistoryItem, ChatSession, Dashboard, PolicySimulationResponse, SecurityPolicy, SecurityPolicyRequest, StoredChatMessage } from './models';

@Injectable({ providedIn: 'root' })
export class ApiService {
  private http = inject(HttpClient);
  private base = environment.apiUrl;

  sendChat(request: ChatRequest) {
    return this.http.post<ChatResponse>(`${this.base}/chat`, request);
  }

  history() {
    return this.http.get<ChatHistoryItem[]>(`${this.base}/chat/history`);
  }

  sessions() {
    return this.http.get<ChatSession[]>(`${this.base}/chat/sessions`);
  }

  createSession(title = 'New chat') {
    return this.http.post<ChatSession>(`${this.base}/chat/sessions`, { title });
  }

  sessionMessages(sessionId: string) {
    return this.http.get<StoredChatMessage[]>(`${this.base}/chat/sessions/${sessionId}/messages`);
  }

  dashboard() { return this.http.get<Dashboard>(`${this.base}/dashboard`); }

  simulatePolicy(prompt: string) {
    return this.http.post<PolicySimulationResponse>(`${this.base}/policy-simulator`, { prompt });
  }

  policies() {
    return this.http.get<SecurityPolicy[]>(`${this.base}/policies`);
  }

  createPolicy(request: SecurityPolicyRequest) {
    return this.http.post<SecurityPolicy>(`${this.base}/policies`, request);
  }

  updatePolicy(id: string, request: SecurityPolicyRequest) {
    return this.http.put<SecurityPolicy>(`${this.base}/policies/${id}`, request);
  }

  deletePolicy(id: string) {
    return this.http.delete<void>(`${this.base}/policies/${id}`);
  }

  applications() {
    return this.http.get<AiApplication[]>(`${this.base}/applications`);
  }

  createApplication(request: AiApplicationRequest) {
    return this.http.post<AiApplication>(`${this.base}/applications`, request);
  }

  updateApplication(id: string, request: AiApplicationRequest) {
    return this.http.put<AiApplication>(`${this.base}/applications/${id}`, request);
  }

  deleteApplication(id: string) {
    return this.http.delete<void>(`${this.base}/applications/${id}`);
  }
}
