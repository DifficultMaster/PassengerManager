# PassengerManager

A Passenger Transport Management System (PTMS) featuring a .NET Server, a WPF Client, and a containerized data infrastructure communicating via secure local HTTPS gRPC.

---

## 🇬🇧 English Instructions

### Prerequisites
Ensure the following are installed and running before proceeding:
* **Docker Desktop** (running in the background).
* **Visual Studio 2026** (configured for .NET desktop and web development).
* **.NET 10 SDK**.

### Infrastructure Setup

#### Step 1: Start the Container Infrastructure
This system uses a hybrid architecture. The data stores and message brokers run in Docker containers, while the .NET Server (via Docker if you have an SSL endpoint, otherwise natively) and WPF Client execute natively on the host machine.
Open a PowerShell terminal in the root directory of the repository and run the following commands to ensure a clean environment and start the infrastructure:

    docker compose down -v
    docker compose up -d db redis rabbitmq

#### Step 2: Configure the Database Credentials
To adhere to secure configuration standards, the database password is not committed to source control. You must inject the local development password into the .NET Secret Manager so the server can authenticate with the Docker database.
Navigate to the Server project directory:

    cd PassengerManager.Server

Initialize the secrets vault for the project:

    dotnet user-secrets init

Inject the development connection string:

    dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Host=localhost;Port=5433;Database=PassengerManagerDB;Username=postgres;Password=my_secure_password;Include Error Detail=true"

#### Step 3: Initialize the Database Schema
With the database running and the credentials securely stored in the local vault, apply the Entity Framework Core migrations to generate the schema and seed the initial tables.
Ensure your terminal is still in the `PassengerManager.Server` directory and run:

    dotnet ef database update

### Running the Application

#### Step 4: Environment Configuration
Depending on the distribution format, a `.env` file may or may not be included in the archive. 
* If a `.env` file **is provided**, ensure it is placed in the root directory of the repository before starting the applications. 
* If **not provided**, the system will fall back to the default configuration values defined in `appsettings.json` and the local user secrets initialized in Step 2.

#### Step 5: Start the Server
The gRPC server must be running before the WPF client is launched. From the `PassengerManager.Server` directory, start the backend:

    dotnet run

*(Alternatively, set the Server project as the Startup Project in Visual Studio and press F5).*

#### Step 6: Start the WPF Client
Once the server is listening for connections, open a new terminal window, navigate to the Client project directory, and launch the desktop application:

    cd ../PassengerManager.Client
    dotnet run

*(Alternatively, configure Visual Studio to start "Multiple Startup Projects" to launch both simultaneously).*

---

## 🇺🇦 Інструкція українською

### Попередні вимоги
Переконайтеся, що перед початком встановлено та запущено:
* **Docker Desktop** (працює у фоновому режимі).
* **Visual Studio 2026** (налаштована для розробки класичних .NET та веб-додатків).
* **.NET 10 SDK**.

### Налаштування інфраструктури

#### Крок 1: Запуск інфраструктури контейнерів
Ця система використовує гібридну архітектуру. Сховища даних та брокери повідомлень працюють у контейнерах Docker, тоді як .NET сервер (на хост-машині, якщо немає сертифікату SSL для зв'язку, і на Docker, якщо навпаки) і WPF клієнт виконуються безпосередньо на хост-машині.
Відкрийте термінал PowerShell у кореневому каталозі репозиторію та виконайте наступні команди:

    docker compose down -v
    docker compose up -d db redis rabbitmq

#### Крок 2: Налаштування облікових даних бази даних
Пароль від бази даних не зберігається в системі контролю версій. Необхідно додати локальний пароль розробника до Secret Manager, щоб сервер міг підключитися до бази даних у Docker.
Перейдіть до каталогу проекту сервера:

    cd PassengerManager.Server

Ініціалізуйте сховище секретів для проекту:

    dotnet user-secrets init

Додайте рядок підключення для розробки:

    dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Host=localhost;Port=5433;Database=PassengerManagerDB;Username=postgres;Password=my_secure_password;Include Error Detail=true"

#### Крок 3: Ініціалізація схеми бази даних
З працюючою базою даних і налаштованими секретами, застосуйте міграції Entity Framework Core для створення схеми та заповнення початкових таблиць.
Переконайтеся, що ви все ще у каталозі `PassengerManager.Server`, та виконайте:

    dotnet ef database update

### Запуск програми

#### Крок 4: Налаштування середовища (Файл .env)
Залежно від формату розповсюдження, архів може містити або не містити файл `.env`.
* Якщо файл `.env` **надається**, переконайтеся, що він знаходиться у кореневому каталозі репозиторію перед запуском додатків.
* Якщо **не надається**, система автоматично використає значення за замовчуванням, визначені в `appsettings.json`, та локальні секрети користувача, ініціалізовані на Кроці 2.

#### Крок 5: Запуск сервера
gRPC сервер має бути запущений до того, як ви відкриєте WPF клієнт. З каталогу `PassengerManager.Server` запустіть бекенд:

    dotnet run

*(Або зробіть проект Server стартовим у Visual Studio і натисніть F5).*

#### Крок 6: Запуск WPF клієнта
Як тільки сервер почне приймати з'єднання, відкрийте нове вікно терміналу, перейдіть до каталогу клієнтського проекту та запустіть десктопний додаток:

    cd ../PassengerManager.Client
    dotnet run

*(Або налаштуйте Visual Studio на "Multiple Startup Projects", щоб запускати обидва проекти одночасно).*
