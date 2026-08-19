# Carotte.Sample Messaging Specification

### Messaging Topology

```mermaid
graph LR
    subgraph Microservice
        NotificationMessage_Publisher["NotificationMessage Publisher"]
        OrderCreated_Publisher["OrderCreated Publisher"]
        MultiMessageConsumer["MultiMessageConsumer"]
        OrderConsumer["OrderConsumer"]
    end

    exch_notifications_exchange[("notifications-exchange")]
    NotificationMessage_Publisher -->|"NotificationMessage"| exch_notifications_exchange
    exch_orders_exchange[("orders-exchange")]
    OrderCreated_Publisher -->|"OrderCreated"| exch_orders_exchange

    queue_order_processing_queue[["order-processing-queue"]]
    queue_order_processing_queue --> MultiMessageConsumer
    exch_orders_exchange -->|"order.created"| queue_order_processing_queue
    dlx_x_dlx_order_processing_queue[("x.dlx.order-processing-queue")]
    queue_order_processing_queue -.->|"DLX"| dlx_x_dlx_order_processing_queue
    dlq_q_dlq_order_processing_queue[["q.dlq.order-processing-queue"]]
    dlx_x_dlx_order_processing_queue --> dlq_q_dlq_order_processing_queue
    queue_order_processing_queue --> OrderConsumer
```

### Produced Messages

| Message | Broker | Exchange | Routing Key | Exchange Type |
| :--- | :--- | :--- | :--- | :--- |
| `NotificationMessage` | `my-broker` | `notifications-exchange` | `NotificationMessage` | `Direct` |
| `OrderCreated` | `my-broker` | `orders-exchange` | `OrderCreated` | `Direct` |

### Consumed Messages

| Message | Consumer | Queue | Broker | Bindings | Error Strategy |
| :--- | :--- | :--- | :--- | :--- | :--- |
| `OrderCreated`<br/>`NotificationMessage` | `MultiMessageConsumer` | `order-processing-queue` | `my-broker` | `orders-exchange` (key: `order.created`, Direct) | 3 retries, DLX: `x.dlx.order-processing-queue` |
| `OrderCreated` | `OrderConsumer` | `order-processing-queue` | `my-broker` | `orders-exchange` (key: `order.created`, Direct) | 3 retries, DLX: `x.dlx.order-processing-queue` |

### Data Contracts

#### `NotificationMessage`

*Represents a notification sent to a customer.*

| Property | Type | Description |
| :--- | :--- | :--- |
| `OrderId` | `Guid` | The related order identifier. |
| `Message` | `string` | The text message content. |
| `RecipientEmail` | `string` | The recipient email address. |

#### `OrderCreated`

*Represents an event triggered when a new order is placed.*

| Property | Type | Description |
| :--- | :--- | :--- |
| `OrderId` | `Guid` | The unique identifier of the created order. |
| `CustomerName` | `string` | The full name of the customer. |
| `Amount` | `decimal` | The total monetary amount for the order. |

