import { Component } from "@angular/core";
import { RouterLink, RouterLinkActive, RouterOutlet } from "@angular/router";

@Component({
  selector: "app-root",
  standalone: true,
  imports: [RouterOutlet, RouterLink, RouterLinkActive],
  templateUrl: "./app.component.html",
})
export class AppComponent {
  nav = [
    { path: "/", label: "Chat", icon: "◉" },
    { path: "/applications", label: "Applications", icon: "▧" },
    { path: "/policy-simulator", label: "Policy Simulator", icon: "◎" },
    { path: "/policies", label: "Policies", icon: "▤" },
    { path: "/dashboard", label: "Dashboard", icon: "▦" },
  ];
}
