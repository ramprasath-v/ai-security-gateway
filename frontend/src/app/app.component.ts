import { Component } from "@angular/core";
import { RouterLink, RouterLinkActive, RouterOutlet } from "@angular/router";

@Component({
  selector: "app-root",
  standalone: true,
  imports: [RouterOutlet, RouterLink, RouterLinkActive],
  template: ` <div class="shell">
    <aside class="sidebar">
      <a class="brand" routerLink="/">
        <span class="brand-mark">AI</span>
        <span>Security Gateway</span>
      </a>
      <nav>
        @for (item of nav; track item.path) {
          <a
            [routerLink]="item.path"
            routerLinkActive="active"
            [routerLinkActiveOptions]="{ exact: item.path === '/' }"
          >
            <span>{{ item.icon }}</span
            >{{ item.label }}
          </a>
        }
      </nav>
    </aside>
    <main>
      <header>
        <div>
          <span class="eyebrow">AI SECURITY</span>
          <h1>AI Security Gateway</h1>
        </div>
        <a routerLink="/dashboard" class="button secondary">Dashboard</a>
      </header>
      <router-outlet />
    </main>
  </div>`,
})
export class AppComponent {
  nav = [
    { path: "/", label: "Chat", icon: "◉" },
    { path: "/dashboard", label: "Dashboard", icon: "▦" },
  ];
}
