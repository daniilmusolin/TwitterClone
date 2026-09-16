# Twitter Clone (MVP)

A learning Twitter clone written in C#. The goal was to **ship a working MVP 
fast**, so the code was deliberately written without regard for scalability 
or best practices.

## ⚠️ Important

This is **not production-ready**. Data is stored in application memory and is 
**lost on every restart**. The architecture doesn't scale, and the code is not 
meant to be an example of good practices. It's a functional demo, not a 
reference implementation.

## Features

- User registration and login
- Posting tweets
- Feed of tweets
- Real-time updates via SignalR (Hubs)
- REST API with DTOs
- Custom middleware

## Tech Stack

- **Backend:** C# / ASP.NET Core
- **Storage:** in-memory (application memory)
- **Real-time:** SignalR
- **Database:** none
- **Cache:** none (no Redis)

## Deliberate Simplifications

| Aspect | Decision | Why |
|---|---|---|
| Storage | In-memory | Skip DB setup entirely |
| Cache | None | Data is already in memory |
| Database | None | MVP doesn't need persistence |
| Scalability | Not designed for it | Goal is a prototype |
| Frontend | No best practices | Speed over code quality |

## Project Structure
<img width="1920" height="1080" alt="image" src="https://github.com/user-attachments/assets/dec3e6ab-176f-4854-a75d-e2ddd7bdf766" />
<img width="595" height="230" alt="image" src="https://github.com/user-attachments/assets/3e8f6ed9-2af0-46c5-8bd9-f53254317416" />
<img width="602" height="154" alt="image" src="https://github.com/user-attachments/assets/8c4fe21e-ec97-436c-b57e-56f5f684bc36" />
<img width="1920" height="1080" alt="image" src="https://github.com/user-attachments/assets/d4b42c2a-c59e-4c66-b326-cf38e9931cfb" />
<img width="1920" height="1080" alt="image" src="https://github.com/user-attachments/assets/94965b5d-308a-4066-94f4-303113b474d0" />
<img width="1920" height="1080" alt="image" src="https://github.com/user-attachments/assets/9826075d-0a16-4b70-9501-11c10bc4c356" />




