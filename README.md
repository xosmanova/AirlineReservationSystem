# 🛫 Speech-Enabled Airline Reservation System

A modern, high-performance Airline Reservation and Flight Management Platform powered by **.NET 10**, **Microsoft SQL Server**, and browser **Web Speech API**.

Developed as a Final Project at Code Academy (Project 19).

---

## 🏛️ System Architecture

This project follows a **Hybrid Architecture** designed with Clean Architecture principles:

- **Central Backend API:** ASP.NET Core Web API (.NET 10) + Entity Framework Core (SQL Server) + Scalar / Swagger API Documentation.
- **Customer Web Application (Variant A):** Modern Vanilla JavaScript (ES6+), Fetch API, Browser Web Speech API for voice-driven flight search, and interactive aircraft seat map.
- **Admin Management Portal (Variant B):** ASP.NET Core MVC (.NET 10) communicating with the central API via typed `HttpClient` services.

```
                      ┌─────────────────────────────────────────┐
                      │        Customer Web (Variant A)         │
                      │       HTML5 + CSS + JavaScript          │
                      │  (Web Speech API, Seat Map, fetch())    │
                      └────────────────────┬────────────────────┘
                                           │
                                           │ JSON / fetch()
                                           ▼
┌───────────────────────┐       ┌───────────────────────────────┐
│ Admin Portal (Variant B)       │      CENTRAL RESTful API      │
│   ASP.NET Core MVC    │──────>│     ASP.NET Core Web API      │
│ (Razor + HttpClient)  │ HTTP  │ (JWT, Business Logic, Scalar) │
└───────────────────────┘       └──────────────┬────────────────┘
                                               │ EF Core
                                               ▼
                                 ┌──────────────────────────────┐
                                 │     Microsoft SQL Server     │
                                 └──────────────────────────────┘
```

---

## ✨ Core Features (Mandatory 8 Features)

- **F1 (Flight Search):** Multi-criteria flight search by origin, destination, travel dates, and passenger count.
- **F2 (Voice Input):** Hands-free speech-to-text flight search using browser Web Speech API.
- **F3 (Flight Listing):** Rich flight search results with multi-attribute filtering (price, airline, duration, stops).
- **F4 (Interactive Seat Map):** Visual interactive aircraft cabin layout (Business & Economy classes) with real-time seat selection.
- **F5 (Booking Flow):** Secure passenger booking creation and atomic seat reservation.
- **F6 (PNR Generation):** Automatic 6-character unique alphanumeric Passenger Name Record (PNR) issuance.
- **F7 (My Trips Dashboard):** Passenger booking tracking, PNR lookup, and reservation cancellation.
- **F8 (Admin Management Panel):** Centralized administration for flights, routes, aircrafts, seat inventories, and bookings.

---

## 🛠️ Technology Stack

- **Framework:** .NET 10 (C# 13/14)
- **Database:** Microsoft SQL Server with Entity Framework Core 10
- **API Documentation:** Scalar API Reference & Swagger (OpenAPI)
- **Authentication:** ASP.NET Core Identity & JWT Bearer Tokens
- **Validation & Mapping:** FluentValidation & AutoMapper
- **PDF Generation:** QuestPDF with QRCoder
- **Frontend Clients:**
  - Client Site: HTML5, CSS3, ES6+ JavaScript, Web Speech API
  - Admin Site: ASP.NET Core MVC (.NET 10), Razor, Typed HttpClient
