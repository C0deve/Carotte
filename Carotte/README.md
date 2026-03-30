Ce projet est un wrapper du client rabbitmq pour la communication entre les microservices.
Ajoute le support d'openTelemetry.
Fournit les interfaces IConsumer<TMessage> et IProducer<TMessage>.
IConsumer<Message>{ Task HandleAsync(TMessage message, CancellationToken cancellationToken)}
Exemple de consumer : 
    MessageConsumer: IConsumer<Message1>, IConsumer<Message2>.
Ajoute le support de la configuration des connexions aux brokers, des exchanges et des queues.
Ajoute l'enregistrement automatique des consumers et producers dans le container de DI.
Une seule connexion par broker.
