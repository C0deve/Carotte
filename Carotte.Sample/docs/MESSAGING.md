# Carotte.Sample Messaging Specification

### Messaging Topology

```mermaid
graph LR
    subgraph Microservice
        OrderCancelledEvent_Publisher["OrderCancelledEvent Publisher"]
        OrderPlacedEvent_Publisher["OrderPlacedEvent Publisher"]
        OrderProcessedEvent_Publisher["OrderProcessedEvent Publisher"]
        NotificationConsumer["NotificationConsumer"]
        OrderAuditConsumer["OrderAuditConsumer"]
        OrderProcessingConsumer["OrderProcessingConsumer"]
    end

    exch_x_pub_order_cancelled[("x.pub.order-cancelled")]
    OrderCancelledEvent_Publisher --> exch_x_pub_order_cancelled
    exch_x_pub_order_placed[("x.pub.order-placed")]
    OrderPlacedEvent_Publisher --> exch_x_pub_order_placed
    exch_x_pub_order_processed[("x.pub.order-processed")]
    OrderProcessedEvent_Publisher --> exch_x_pub_order_processed

    queue_q_notification_consumer[["q.notification-consumer"]]
    queue_q_notification_consumer --> NotificationConsumer
    exch_x_sub_notification_consumer[("x.sub.notification-consumer")]
    exch_x_sub_notification_consumer --> queue_q_notification_consumer
    exch_x_pub_order_processed --> exch_x_sub_notification_consumer
    dlx_x_dlx_notification_consumer[("x.dlx.notification-consumer")]
    queue_q_notification_consumer -.->|"DLX"| dlx_x_dlx_notification_consumer
    dlq_q_dlq_notification_consumer[["q.dlq.notification-consumer"]]
    dlx_x_dlx_notification_consumer --> dlq_q_dlq_notification_consumer
    queue_q_order_audit_consumer[["q.order-audit-consumer"]]
    queue_q_order_audit_consumer --> OrderAuditConsumer
    exch_x_sub_order_audit_consumer[("x.sub.order-audit-consumer")]
    exch_x_sub_order_audit_consumer --> queue_q_order_audit_consumer
    exch_x_pub_order_placed --> exch_x_sub_order_audit_consumer
    exch_x_pub_order_processed --> exch_x_sub_order_audit_consumer
    exch_x_pub_order_cancelled --> exch_x_sub_order_audit_consumer
    dlx_x_dlx_order_audit_consumer[("x.dlx.order-audit-consumer")]
    queue_q_order_audit_consumer -.->|"DLX"| dlx_x_dlx_order_audit_consumer
    dlq_q_dlq_order_audit_consumer[["q.dlq.order-audit-consumer"]]
    dlx_x_dlx_order_audit_consumer --> dlq_q_dlq_order_audit_consumer
    queue_q_order_processing_consumer[["q.order-processing-consumer"]]
    queue_q_order_processing_consumer --> OrderProcessingConsumer
    exch_x_sub_order_processing_consumer[("x.sub.order-processing-consumer")]
    exch_x_sub_order_processing_consumer --> queue_q_order_processing_consumer
    exch_x_pub_order_placed --> exch_x_sub_order_processing_consumer
    dlx_x_dlx_order_processing_consumer[("x.dlx.order-processing-consumer")]
    queue_q_order_processing_consumer -.->|"DLX"| dlx_x_dlx_order_processing_consumer
    dlq_q_dlq_order_processing_consumer[["q.dlq.order-processing-consumer"]]
    dlx_x_dlx_order_processing_consumer --> dlq_q_dlq_order_processing_consumer
```

### Produced Messages

| Message | Broker | Exchange | Routing Key | Exchange Type |
| :--- | :--- | :--- | :--- | :--- |
| `OrderCancelledEvent` | - | `x.pub.order-cancelled` | - | `Fanout` |
| `OrderPlacedEvent` | - | `x.pub.order-placed` | - | `Fanout` |
| `OrderProcessedEvent` | - | `x.pub.order-processed` | - | `Fanout` |

### Consumed Messages

| Message | Consumer | Queue | Broker | Bindings | Error Strategy |
| :--- | :--- | :--- | :--- | :--- | :--- |
| `OrderProcessedEvent` | `NotificationConsumer` | `q.notification-consumer` | - | `x.pub.order-processed` &rarr; `x.sub.notification-consumer` | 3 retries, DLX: `x.dlx.notification-consumer` |
| `OrderPlacedEvent`<br/>`OrderProcessedEvent`<br/>`OrderCancelledEvent` | `OrderAuditConsumer` | `q.order-audit-consumer` | - | `x.pub.order-placed` &rarr; `x.sub.order-audit-consumer`<br/>`x.pub.order-processed` &rarr; `x.sub.order-audit-consumer`<br/>`x.pub.order-cancelled` &rarr; `x.sub.order-audit-consumer` | 3 retries, DLX: `x.dlx.order-audit-consumer` |
| `OrderPlacedEvent` | `OrderProcessingConsumer` | `q.order-processing-consumer` | - | `x.pub.order-placed` &rarr; `x.sub.order-processing-consumer` | 3 retries, DLX: `x.dlx.order-processing-consumer` |

### Data Contracts

#### `OrderCancelledEvent`

*Represents an event published when an order is cancelled.*

| Property | Type | Description |
| :--- | :--- | :--- |
| `OrderId` | `Guid` | The unique identifier of the cancelled order. |
| `Reason` | `string` | The cancellation reason. |
| `CancelledAt` | `DateTimeOffset` | The timestamp when the order was cancelled. |

#### `OrderPlacedEvent`

*Represents an event published when a customer places a new order.*

| Property | Type | Description |
| :--- | :--- | :--- |
| `OrderId` | `Guid` | The unique identifier of the order. |
| `CustomerId` | `Guid` | The unique identifier of the customer. |
| `CustomerEmail` | `string` | The email address of the customer. |
| `TotalAmount` | `decimal` | The total amount of the order. |
| `PlacedAt` | `DateTimeOffset` | The timestamp when the order was placed. |

#### `OrderProcessedEvent`

*Represents an event published when an order has been successfully processed.*

| Property | Type | Description |
| :--- | :--- | :--- |
| `OrderId` | `Guid` | The unique identifier of the order. |
| `CustomerId` | `Guid` | The unique identifier of the customer. |
| `CustomerEmail` | `string` | The email address of the customer. |
| `TotalAmount` | `decimal` | The total amount processed. |
| `ProcessedAt` | `DateTimeOffset` | The timestamp when the order was processed. |

