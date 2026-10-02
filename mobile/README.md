# RentApp mobile

This is the Expo (SDK 57) and React Native app for PG owners and managers. Setup and phone networking
are covered in [../DEVELOPMENT.md](../DEVELOPMENT.md), and the code structure in
[../ARCHITECTURE.md](../ARCHITECTURE.md#mobile).

```powershell
npm install
npx expo start        # scan the QR code with Expo Go
npm test              # Jest and React Native Testing Library
npm run typecheck
npm run lint
```

Always add packages with `npx expo install <pkg>`, so the versions match the SDK. The app runs in
**Expo Go**, so only libraries that are bundled in Expo Go can be used without switching to a
development build.
