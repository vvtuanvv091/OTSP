import AppRoutes from '../routes/AppRoutes';
import AppRequestFeedback from '../components/feedback/AppRequestFeedback';
import AiAssistantWidget from '../components/common/AiAssistantWidget';

export default function App() {
  return (
    <>
      <AppRoutes />
      <AppRequestFeedback />
      <AiAssistantWidget />
    </>
  );
}

