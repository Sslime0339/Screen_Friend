# Архитектура взаимодействия компонентов (C#) тест

```mermaid
sequenceDiagram
    autonumber
    actor User as Пользователь
    participant UI as OrderWindow (WPF)
    participant BL as OrderService (Бизнес-логика)
    participant DB as Database (Entity Framework)

    User->>UI: Нажимает "Создать заказ"
    UI->>BL: CreateOrder(orderData)
    Note over BL: Валидация данных и<br/>расчет скидки
    BL->>DB: SaveChangesAsync()
    DB-->>BL: Успешное сохранение (ID)
    BL-->>UI: Возврат созданного объекта
    UI-->>User: Отображение статуса "Готово"
```
