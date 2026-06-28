import { inject, Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { environment } from '../../environments/environment';
import { ChatRequest, ChatResponse, ChatHistoryItem, ChatSession, Dashboard, StoredChatMessage } from './models';

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
}
